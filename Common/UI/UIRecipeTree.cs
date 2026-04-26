using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Per-row metadata describing which depth-wise connector verticals pass through
    /// this row and whether it originates a branch stub into its icon. Populated by
    /// <see cref="UIRecipeTree.SetTree"/> after the flat row list is built.
    /// </summary>
    public readonly struct ConnectorInfo
    {
        public readonly int[] ThroughDepths;   // full-height verticals for ancestor buses still open
        public readonly int BranchDepth;       // -1 if row owns no branch (root, condition line, non-member)
        public readonly bool IsLastAtBranch;   // true when this row terminates its parent's bus
        public readonly bool DrawBranchStub;   // false for condition lines (through-only)

        public ConnectorInfo(int[] through, int branchDepth, bool isLast, bool drawStub)
        {
            ThroughDepths = through;
            BranchDepth = branchDepth;
            IsLastAtBranch = isLast;
            DrawBranchStub = drawStub;
        }
    }

    /// <summary>
    /// Marker interface implemented by every row type produced by <see cref="UIRecipeTree"/>
    /// so the shared post-build walk can inject connector metadata uniformly.
    /// </summary>
    public interface IConnectorTarget
    {
        void SetConnectorInfo(ConnectorInfo info);
    }

    public class UIRecipeTree : UIElement
    {
        private const string EmptyStateText = "Click an item above to view its recipe tree.";
        private UIList _list;
        private UIScrollbar _scrollbar;
        private UIEmptyStatePlaceholder _placeholder;
        private RecipeTreeNode _currentRoot;
        private readonly HashSet<int> _collapsedItemIds = new();
        private static readonly Dictionary<int, int> TileDisplayItemCache = new();

        // Per-frame scan lookup for ingredient rows (avoids rebuilding the tree on scan change).
        private Func<int, int> _getHaveCount;

        private const float DepthIndent = 38f;
        private const float RowPadding = 6f;
        private const float ArrowColumnWidth = 14f;
        private const float IconBoxSize = 30f;
        private const float IconInnerSize = 26f;
        private const float NodeTextSpacing = 8f;
        private const float IngredientExtraIndent = 32f;

        // Shared connector geometry.
        internal const float ConnectorStrokeWidth = 2f;
        internal const float ConnectorListGapBridge = 1f; // matches `_list.ListPadding`
        private const float TreeItemBaseRowHeight = 38f;

        /// <summary>
        /// X (screen-space) of the vertical that binds siblings rendered at <paramref name="depth"/>.
        /// For a child rendered at depth d, this is the left edge of the child's arrow column —
        /// one column to the left of the child's icon box.
        /// </summary>
        internal static int ConnectorVerticalX(float rowLeftX, int depth)
        {
            return SnapToPixel(rowLeftX + (depth + 1) * DepthIndent - ArrowColumnWidth);
        }

        /// <summary>
        /// X (screen-space) of the icon box left edge at <paramref name="depth"/>. Used to
        /// terminate the branch stub just before the icon's border.
        /// </summary>
        internal static int ConnectorIconLeftX(float rowLeftX, int depth)
        {
            return SnapToPixel(rowLeftX + (depth + 1) * DepthIndent);
        }

        /// <summary>
        /// Draws this row's connector lines: a full-height vertical for every ancestor bus still
        /// open (<see cref="ConnectorInfo.ThroughDepths"/>), plus the branch stub at
        /// <see cref="ConnectorInfo.BranchDepth"/> terminating in the icon centerY. The branch
        /// vertical's extent depends on <see cref="ConnectorInfo.IsLastAtBranch"/>:
        /// full-height-plus-bridge for non-terminators, rowTop→centerY for terminators.
        /// </summary>
        internal static void DrawConnectors(SpriteBatch spriteBatch, CalculatedStyle dims, ConnectorInfo info, float iconCenterY)
        {
            if (info.ThroughDepths == null && info.BranchDepth < 0)
                return;

            int rowTop = SnapToPixel(dims.Y);
            int rowHeight = SnapToPixel(dims.Y + dims.Height) - rowTop;
            float rowLeftX = dims.X;

            if (info.ThroughDepths != null)
            {
                for (int i = 0; i < info.ThroughDepths.Length; i++)
                {
                    int d = info.ThroughDepths[i];
                    int vx = ConnectorVerticalX(rowLeftX, d);
                    DrawConnectorVertical(spriteBatch, vx, rowTop, rowHeight + (int)ConnectorListGapBridge);
                }
            }

            if (info.BranchDepth >= 0)
            {
                int vx = ConnectorVerticalX(rowLeftX, info.BranchDepth);

                if (info.IsLastAtBranch)
                {
                    int snappedCenterY = SnapToPixel(iconCenterY);
                    int height = Math.Max(0, snappedCenterY - rowTop);
                    DrawConnectorVertical(spriteBatch, vx, rowTop, height);
                }
                else
                {
                    DrawConnectorVertical(spriteBatch, vx, rowTop, rowHeight + (int)ConnectorListGapBridge);
                }

                if (info.DrawBranchStub)
                {
                    int stubLeft = vx + (int)ConnectorStrokeWidth;
                    int stubRight = ConnectorIconLeftX(rowLeftX, info.BranchDepth);
                    int stubY = SnapToPixel(iconCenterY) - 1;
                    int stubWidth = Math.Max(0, stubRight - stubLeft);
                    if (stubWidth > 0)
                    {
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                            new Rectangle(stubLeft, stubY, stubWidth, (int)ConnectorStrokeWidth),
                            UIPalette.TreeConnector);
                    }
                }
            }
        }

        private static void DrawConnectorVertical(SpriteBatch spriteBatch, int x, int y, int height)
        {
            if (height <= 0)
                return;
            spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(x, y, (int)ConnectorStrokeWidth, height),
                UIPalette.TreeConnector);
        }

        public override void OnInitialize()
        {
            var bg = new UIPanel();
            bg.Width.Set(0f, 1f);
            bg.Height.Set(0f, 1f);
            bg.SetPadding(6f);
            bg.BackgroundColor = new Color(0, 0, 0, 0); // let the recipe column backdrop show through
            bg.BorderColor = new Color(0, 0, 0, 0);
            Append(bg);

            _scrollbar = new UIScrollbar();
            _scrollbar.Height.Set(-12f, 1f);
            _scrollbar.Top.Set(6f, 0f);
            _scrollbar.Left.Set(-22f, 1f);
            bg.Append(_scrollbar);

            _list = new UIList();
            // UIList.Add sorts _items via List<T>.Sort on every insertion; the default
            // UIElement.CompareTo returns 0, and List<T>.Sort is unstable past the
            // 16-element introsort threshold, which scrambles long recipe trees.
            // We add rows in render order so disable sorting outright.
            _list.ManualSortMethod = _ => { };
            _list.Width.Set(-28f, 1f);
            _list.Height.Set(0f, 1f);
            _list.ListPadding = 1f;
            _list.SetScrollbar(_scrollbar);
            bg.Append(_list);

            _placeholder = new UIEmptyStatePlaceholder(EmptyStateText, Color.Gray, 0.75f, () => _currentRoot == null);
            _placeholder.Width.Set(-28f, 1f);
            _placeholder.Height.Set(0f, 1f);
            bg.Append(_placeholder);

            ShowPlaceholder();
        }

        public static void ClearCaches()
        {
            TileDisplayItemCache.Clear();
        }

        /// <summary>
        /// Sets the lookup used by ingredient rows to pull `have` counts each frame.
        /// Lookup receives an item id and returns the available stack (0 if missing).
        /// </summary>
        public void SetHaveLookup(Func<int, int> getHaveCount)
        {
            _getHaveCount = getHaveCount;
        }

        public void ClearTree()
        {
            _currentRoot = null;
            _list?.Clear();
            ShowPlaceholder();
        }

        public void SetTree(RecipeTreeNode root)
        {
            _currentRoot = root;

            _list.Clear();

            if (root == null)
            {
                ShowPlaceholder();
                return;
            }

            var entries = new List<RowEntry>();
            var buses = new List<BusFrame>();
            var openBusIndexStack = new List<int>(); // stack of bus frame indices currently open

            // Root title row — no connector (empty info).
            var rootStations = root.UsedRecipe != null ? ResolveStations(root.UsedRecipe) : new List<StationDisplayInfo>();
            var rootChip = BuildStatusChip(root.Status);
            // Color.Gold is a fallback only — UITreeItemLine.DrawSelf reads the live rarity color
            // every frame so dynamic rarities (Master/Expert/Calamity Auric/Cosmilite) animate.
            var rootLine = new UITreeItemLine(root.ItemId, string.Empty, Color.Gold, 0.8f, -1, TriangleState.None, rootStations, rootChip);
            rootLine.Width.Set(0f, 1f);
            rootLine.Height.Set(TreeItemBaseRowHeight, 0f);
            EmitRow(entries, openBusIndexStack, rootLine, branchDepth: -1);
            _list.Add(rootLine);

            // Condition line emission disabled — shimmer/transmutation conditions rendered as
            // "Conditions: …" rows confused users into treating them as craftable recipes.
            // if (root.UsedRecipe != null)
            //     EmitConditionLine(entries, openBusIndexStack, root.UsedRecipe, -1);

            if (root.UsedRecipe != null || (root.Children != null && root.Children.Count > 0))
            {
                // Open root's child bus at depth 0. Ingredients and expandable children of the
                // root share this bus. The bus wraps BOTH AddIngredientRows and AddChildren.
                int rootBusIndex = OpenBus(buses, openBusIndexStack, depth: 0);

                if (root.UsedRecipe != null)
                    EmitIngredientRows(entries, openBusIndexStack, root, depth: 0);

                EmitChildren(entries, buses, openBusIndexStack, root, parentDepth: -1);

                CloseBus(buses, openBusIndexStack, rootBusIndex);
            }

            ResolveConnectorMetadata(entries, buses);
        }

        /// <summary>Records a single row in the walk. Captures open-bus snapshot + branch depth.</summary>
        private void EmitRow(List<RowEntry> entries, List<int> openBusIndexStack, IConnectorTarget row, int branchDepth)
        {
            // Snapshot currently-open bus indices (the stack order doesn't matter for resolve;
            // we just need to know which buses were live at emit time).
            int[] openSnapshot = openBusIndexStack.Count == 0
                ? Array.Empty<int>()
                : openBusIndexStack.ToArray();

            entries.Add(new RowEntry
            {
                Row = row,
                BranchDepth = branchDepth,
                OpenBusesAtEmit = openSnapshot,
                RowIndex = entries.Count,
            });
        }

        private int OpenBus(List<BusFrame> buses, List<int> openBusIndexStack, int depth)
        {
            int index = buses.Count;
            buses.Add(new BusFrame { Depth = depth, LastBranchRowIndex = -1 });
            openBusIndexStack.Add(index);
            return index;
        }

        private void CloseBus(List<BusFrame> buses, List<int> openBusIndexStack, int busIndex)
        {
            // Pop the bus. Buses nest perfectly so the top of stack should match `busIndex`.
            int topIdx = openBusIndexStack.Count - 1;
            if (topIdx >= 0 && openBusIndexStack[topIdx] == busIndex)
                openBusIndexStack.RemoveAt(topIdx);
        }

        private void EmitChildren(List<RowEntry> entries, List<BusFrame> buses, List<int> openBusIndexStack,
            RecipeTreeNode node, int parentDepth)
        {
            if (node.Children == null || node.Children.Count == 0)
                return;

            // Only children with their own expandable sub-recipe are rendered as tree rows.
            // Leaf ingredients (no sub-recipe) are represented by the parent's flat ingredient
            // rows, matching the HTML design's disjoint `children` / `ingredients` split.
            var expandableChildren = new List<RecipeTreeNode>();
            foreach (var child in node.Children)
            {
                if (HasDisplayableRecipeChildren(child))
                    expandableChildren.Add(child);
            }

            if (expandableChildren.Count == 0)
                return;

            int childDepth = parentDepth + 1;

            foreach (var child in expandableChildren)
            {
                string countStr = child.RequiredCount > 1 ? $" x{child.RequiredCount}" : string.Empty;

                // NodeStatus (Owned/Craftable/Missing) is no longer encoded in the name color —
                // it surfaces via the right-side status chip (OWNED/MISSING) and the owned-count
                // label. The name color is driven by item rarity at Draw time so it matches the
                // in-game inventory/tooltip color (including dynamic shifting rarities).
                Color fallbackColor = UIPalette.CellNameText;

                bool isCollapsed = IsCollapsed(child);
                TriangleState triangleState = isCollapsed ? TriangleState.Collapsed : TriangleState.Expanded;

                var stations = ResolveStations(child.UsedRecipe);
                var chip = BuildStatusChip(child, hasRecipeDetails: true);
                var line = new UITreeItemLine(child.ItemId, countStr, fallbackColor, 0.65f,
                    childDepth, triangleState, stations, chip,
                    _getHaveCount,
                    child.Status == NodeStatus.Craftable);
                line.Width.Set(0f, 1f);
                line.Height.Set(TreeItemBaseRowHeight, 0f);

                var capturedChild = child;
                line.OnLeftClick += (evt, el) => ToggleCollapse(capturedChild.ItemId);

                // Child row branches at childDepth (the parent's child-bus depth).
                EmitRow(entries, openBusIndexStack, line, branchDepth: childDepth);
                _list.Add(line);

                if (!isCollapsed)
                {
                    int grandBusIndex = OpenBus(buses, openBusIndexStack, depth: childDepth + 1);

                    EmitIngredientRows(entries, openBusIndexStack, child, depth: childDepth + 1);
                    // EmitConditionLine(entries, openBusIndexStack, child.UsedRecipe, childDepth);
                    EmitChildren(entries, buses, openBusIndexStack, child, parentDepth: childDepth);

                    CloseBus(buses, openBusIndexStack, grandBusIndex);
                }
            }
        }

        private void EmitIngredientRows(List<RowEntry> entries, List<int> openBusIndexStack,
            RecipeTreeNode node, int depth)
        {
            if (node?.UsedRecipe == null)
                return;

            // Ingredients that will render as expandable tree children must NOT also appear
            // as flat rows — the HTML design treats `children` and `ingredients` as disjoint sets.
            var expandableChildIds = new HashSet<int>();
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    if (HasDisplayableRecipeChildren(child))
                        expandableChildIds.Add(child.ItemId);
                }
            }

            int batchSize = Math.Max(1, node.UsedRecipe.createItem.stack);
            int needed = Math.Max(1, node.RequiredCount);
            int batches = (needed + batchSize - 1) / batchSize;

            // Indent matches HTML: (6 + level*18 + 32). `depth` is the depth of the block
            // (one below its parent node), so multiply by DepthIndent then add RowPadding
            // (to align with the parent row's contentX) + IngredientExtraIndent.
            float leftIndent = depth * DepthIndent + RowPadding + IngredientExtraIndent;

            var blockRows = new List<UIIngredientRow>();
            foreach (var ingredient in node.UsedRecipe.requiredItem)
            {
                if (ingredient.type <= ItemID.None)
                    continue;

                if (expandableChildIds.Contains(ingredient.type))
                    continue;

                int ingredientNeeded = ingredient.stack * batches;
                var row = new UIIngredientRow(ingredient.type, ingredientNeeded, _getHaveCount, leftIndent);
                row.Width.Set(0f, 1f);
                row.Height.Set(34f, 0f);
                blockRows.Add(row);
            }

            for (int i = 0; i < blockRows.Count; i++)
            {
                // `_isLastInBlock` still drives the IngRowSeparator hairline between ingredient
                // rows — kept independent of the new connector system.
                blockRows[i].SetLastInBlock(i == blockRows.Count - 1);
                // Ingredient rows branch at `depth` (they live in the parent's child-bus).
                EmitRow(entries, openBusIndexStack, blockRows[i], branchDepth: depth);
                _list.Add(blockRows[i]);
            }
        }

        private void EmitConditionLine(List<RowEntry> entries, List<int> openBusIndexStack,
            Recipe recipe, int textDepth)
        {
            if (recipe == null)
                return;

            if (recipe.Conditions == null || recipe.Conditions.Count == 0)
                return;

            var condNames = new List<string>();
            foreach (var cond in recipe.Conditions)
                condNames.Add(cond.Description.Value);

            string text = $"Conditions: {string.Join(", ", condNames)}";
            var line = new UITreeTextLine(text, new Color(180, 180, 220), 0.65f, textDepth);
            line.Width.Set(0f, 1f);
            line.Height.Set(20f, 0f);
            // Condition lines carry through-verticals only (no branch, no stub).
            EmitRow(entries, openBusIndexStack, line, branchDepth: -1);
            _list.Add(line);
        }

        private void ResolveConnectorMetadata(List<RowEntry> entries, List<BusFrame> buses)
        {
            // First pass: compute `LastBranchRowIndex` per bus = largest RowIndex with
            // BranchDepth == bus.Depth AND that bus open at emit time.
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.BranchDepth < 0)
                    continue;
                for (int k = 0; k < e.OpenBusesAtEmit.Length; k++)
                {
                    int busIdx = e.OpenBusesAtEmit[k];
                    if (buses[busIdx].Depth == e.BranchDepth)
                    {
                        var frame = buses[busIdx];
                        if (e.RowIndex > frame.LastBranchRowIndex)
                        {
                            frame.LastBranchRowIndex = e.RowIndex;
                            buses[busIdx] = frame;
                        }
                        break;
                    }
                }
            }

            // Second pass: compute per-row ConnectorInfo.
            // A row "passes through" bus `b` iff b is open at emit AND has a later branch row
            // (LastBranchRowIndex > this row's index) AND it is not this row's own branch bus.
            var throughBuf = new List<int>(4);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                throughBuf.Clear();

                int branchDepth = e.BranchDepth;
                bool isLastAtBranch = false;
                bool drawStub = e.Row is not UITreeTextLine;

                for (int k = 0; k < e.OpenBusesAtEmit.Length; k++)
                {
                    int busIdx = e.OpenBusesAtEmit[k];
                    var frame = buses[busIdx];

                    if (branchDepth >= 0 && frame.Depth == branchDepth)
                    {
                        // This bus is the row's own branch bus — handled via BranchDepth
                        // (we never double-include it in throughs).
                        isLastAtBranch = (frame.LastBranchRowIndex == e.RowIndex);
                        continue;
                    }

                    if (frame.LastBranchRowIndex > e.RowIndex)
                        throughBuf.Add(frame.Depth);
                }

                int[] throughArr = throughBuf.Count == 0 ? Array.Empty<int>() : throughBuf.ToArray();
                e.Row.SetConnectorInfo(new ConnectorInfo(throughArr, branchDepth, isLastAtBranch, drawStub));
            }
        }

        private struct BusFrame
        {
            public int Depth;
            public int LastBranchRowIndex; // -1 until resolved
        }

        private struct RowEntry
        {
            public IConnectorTarget Row;
            public int BranchDepth;        // -1 for non-branching rows
            public int[] OpenBusesAtEmit;  // bus-frame indices open when the row was emitted
            public int RowIndex;           // emission order (used as tie-break key)
        }

        private static StatusChipInfo BuildStatusChip(NodeStatus status)
        {
            return status switch
            {
                // CRAFTABLE chips are intentionally suppressed in the recipe tree —
                // the tree now communicates craftable state via node color/owned count only.
                NodeStatus.Craftable => default,
                NodeStatus.Owned => new StatusChipInfo(string.Empty,
                    UIPalette.ChipOwnedBg, UIPalette.ChipOwnedBorder, UIPalette.ChipOwnedText),
                _ => new StatusChipInfo("MISSING",
                    UIPalette.ChipMissingBg, UIPalette.ChipMissingBorder, UIPalette.ChipMissingText),
            };
        }

        private static StatusChipInfo BuildStatusChip(RecipeTreeNode node, bool hasRecipeDetails)
        {
            return node.Status switch
            {
                NodeStatus.Owned => new StatusChipInfo($"OWNED x{node.OwnedCount}",
                    UIPalette.ChipOwnedBg, UIPalette.ChipOwnedBorder, UIPalette.ChipOwnedText),
                // CRAFTABLE chips are intentionally suppressed in the recipe tree —
                // intermediate craftable nodes surface an owned-count label on the right edge instead.
                NodeStatus.Craftable => default,
                _ => new StatusChipInfo("MISSING",
                    UIPalette.ChipMissingBg, UIPalette.ChipMissingBorder, UIPalette.ChipMissingText),
            };
        }

        private void ToggleCollapse(int itemId)
        {
            if (!_collapsedItemIds.Remove(itemId))
                _collapsedItemIds.Add(itemId);

            if (_currentRoot != null)
                SetTree(_currentRoot);
        }

        private static bool HasRecipeDetails(RecipeTreeNode node)
        {
            return node?.UsedRecipe != null && node.Children != null;
        }

        private static bool HasDisplayableRecipeChildren(RecipeTreeNode node)
        {
            return HasRecipeDetails(node) && node.Children.Count > 0;
        }

        private bool IsCollapsed(RecipeTreeNode node)
        {
            if (node == null || node.UsedRecipe == null)
                return false;
            return _collapsedItemIds.Contains(node.ItemId);
        }

        private static List<StationDisplayInfo> ResolveStations(Recipe recipe)
        {
            var stations = new List<StationDisplayInfo>();
            foreach (int tileId in recipe.requiredTile)
            {
                if (tileId < 0)
                    continue;

                string tileName = GetTileName(tileId);
                int itemId = ResolveDisplayItemIdForTile(tileId);
                stations.Add(new StationDisplayInfo(tileId, tileName, itemId));
            }

            return stations;
        }

        private static int ResolveDisplayItemIdForTile(int tileId)
        {
            if (TileDisplayItemCache.TryGetValue(tileId, out int cachedItemId))
                return cachedItemId;

            int resolvedItemId = ItemID.None;
            for (int itemId = 1; itemId < ItemLoader.ItemCount; itemId++)
            {
                if (!UIItemRenderingHelper.TryCreateDisplayItem(itemId, out Item item))
                    continue;

                if (item.createTile == tileId)
                {
                    resolvedItemId = itemId;
                    break;
                }
            }

            TileDisplayItemCache[tileId] = resolvedItemId;
            return resolvedItemId;
        }

        private static string GetTileName(int tileId)
        {
            if (TryGetMapObjectName(tileId, out string mapObjectName))
                return mapObjectName;

            ModTile modTile = TileLoader.GetTile(tileId);
            if (modTile != null)
            {
                string localizedMapEntry = modTile.GetLocalizedValue("MapEntry");
                string mapEntryKey = modTile.GetLocalizationKey("MapEntry");
                if (!string.IsNullOrWhiteSpace(localizedMapEntry) &&
                    !string.Equals(localizedMapEntry, mapEntryKey, StringComparison.Ordinal))
                {
                    return localizedMapEntry;
                }
            }

            return $"Tile #{tileId}";
        }

        private static bool TryGetMapObjectName(int tileId, out string tileName)
        {
            tileName = string.Empty;

            try
            {
                int lookup = MapHelper.TileToLookup(tileId, 0);
                string name = Lang.GetMapObjectName(lookup);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    tileName = name;
                    return true;
                }
            }
            catch
            {
                // Some tiles may not expose a map lookup entry. Leave the localized fallback to the caller.
            }

            return false;
        }

        private void ShowPlaceholder()
        {
            _list?.Clear();
        }

        private static Vector2 GetCenteredBorderStringPosition(string text, float leftX, float centerY, float scale)
        {
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * scale;
            return new Vector2(leftX, centerY - textSize.Y * 0.5f);
        }

        private static int SnapToPixel(float value)
        {
            return (int)MathF.Round(value);
        }

        private class UIEmptyStatePlaceholder : UIElement
        {
            private readonly string _text;
            private readonly Color _color;
            private readonly float _scale;
            private readonly Func<bool> _shouldDraw;

            public UIEmptyStatePlaceholder(string text, Color color, float scale, Func<bool> shouldDraw)
            {
                _text = text;
                _color = color;
                _scale = scale;
                _shouldDraw = shouldDraw;
                IgnoresMouseInteraction = true;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                if (_shouldDraw == null || !_shouldDraw())
                    return;

                var dims = GetDimensions();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(_text) * _scale;
                Vector2 position = new(
                    dims.X + (dims.Width - textSize.X) * 0.5f,
                    dims.Y + (dims.Height - textSize.Y) * 0.5f);

                Utils.DrawBorderString(spriteBatch, _text, position, _color, _scale);
            }
        }

        private readonly record struct StationDisplayInfo(int TileId, string DisplayName, int ItemId)
        {
            public bool HasDisplayItem => ItemId > ItemID.None;
        }

        private readonly record struct StatusChipInfo(string Text, Color Background, Color Border, Color TextColor)
        {
            public bool IsVisible => !string.IsNullOrEmpty(Text);
        }

        private enum TriangleState
        {
            None,
            Expanded,
            Collapsed
        }

        /// <summary>
        /// Tree node line rendered as: [arrow column | icon box | name | chip | station badges].
        /// Connector lines bind siblings vertically via <see cref="ConnectorInfo"/> populated
        /// post-build by <see cref="UIRecipeTree.SetTree"/>.
        /// </summary>
        private class UITreeItemLine : UIElement, IConnectorTarget
        {
            private readonly int _itemId;
            private readonly string _suffix;
            private readonly Color _color;
            private readonly float _scale;
            private readonly int _depth;
            private readonly TriangleState _triangleState;
            private readonly List<StationDisplayInfo> _stations;
            private readonly StatusChipInfo _statusChip;
            private readonly Func<int, int> _haveLookup;
            private readonly bool _showOwnedLabel;
            private ConnectorInfo _connector;

            private const float TriangleSize = 8f;
            private const float ArrowCenterOffset = 5f;
            private const float BaseRowHeight = 38f;
            private const float InlineBadgeSpacing = 8f;
            private const float BadgeSize = 24f;
            private const float BadgeSpacing = 6f;
            private const float RowSpacing = 4f;
            private const float RightPadding = 4f;
            private const float FallbackScale = 0.58f;
            private const float MaxFallbackBadgeWidth = 140f;
            private const float ChipHeight = 16f;
            private const float ChipHorizontalPadding = 6f;
            private const float ChipScale = 0.58f;
            private const float OwnedLabelScale = 0.7f;
            private const float OwnedLabelGap = 8f;
            // Right inset for the owned-count label. Matches `UIIngredientRow.RightPadding`
            // so the gray intermediate count and the leaf `have/need` text share the same
            // vertical right edge. Kept independent of `RightPadding` (which governs
            // station badges) so station layout is unaffected.
            private const float OwnedLabelRightPadding = 10f;

            private static readonly Color BadgeHoverColor = UIPalette.StationHoverBg;

            public UITreeItemLine(int itemId, string suffix, Color color, float scale,
                int depth, TriangleState triangleState,
                List<StationDisplayInfo> stations = default,
                StatusChipInfo chip = default,
                Func<int, int> haveLookup = null,
                bool showOwnedLabel = false)
            {
                _itemId = itemId;
                _suffix = suffix;
                _color = color;
                _scale = scale;
                _depth = depth;
                _triangleState = triangleState;
                _stations = stations ?? new List<StationDisplayInfo>();
                _statusChip = chip;
                _haveLookup = haveLookup;
                _showOwnedLabel = showOwnedLabel;
            }

            public void SetConnectorInfo(ConnectorInfo info)
            {
                _connector = info;
            }

            public override void Recalculate()
            {
                base.Recalculate();

                float desiredHeight = CalculateDesiredHeight(GetDimensions().Width);
                if (Math.Abs(Height.Pixels - desiredHeight) > 0.5f)
                {
                    Height.Set(desiredHeight, 0f);
                    base.Recalculate();
                }
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float x = dims.X;
                float y = dims.Y;
                float centerY = y + BaseRowHeight / 2f;

                // Connector lines render first so the icon box (drawn later) cleanly terminates the
                // branch stub. Icon centerY must use BaseRowHeight/2f (top band), NOT dims.Height/2f,
                // so station-badge wrap does not float the stub below the icon.
                UIRecipeTree.DrawConnectors(spriteBatch, dims, _connector, centerY);

                float contentX = GetContentX(x);
                bool rowHovered = dims.ToRectangle().Contains(Main.mouseX, Main.mouseY);

                // Arrow sits in a fixed-width column at the row's left (only for collapsible rows)
                if (_triangleState != TriangleState.None)
                {
                    Color triColor = rowHovered ? UIPalette.TreeArrowHover : UIPalette.TreeArrow;
                    DrawTriangle(spriteBatch,
                        new Vector2(contentX + ArrowCenterOffset, centerY),
                        _triangleState, triColor);
                }

                // Icon box (22x22) — bg + 1px border, with the item icon drawn inside
                float iconBoxLeft = GetIconBoxLeft(x);
                var iconBoxRect = new Rectangle(
                    SnapToPixel(iconBoxLeft),
                    SnapToPixel(centerY - IconBoxSize * 0.5f),
                    (int)IconBoxSize,
                    (int)IconBoxSize);
                UIDrawHelper.DrawRect(spriteBatch, iconBoxRect, UIPalette.TreeIconBg);
                UIDrawHelper.DrawBorder(spriteBatch, iconBoxRect, UIPalette.TreeIconBorder, 1);
                UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, _itemId,
                    new Vector2(iconBoxRect.X + IconBoxSize * 0.5f, iconBoxRect.Y + IconBoxSize * 0.5f),
                    IconInnerSize);

                // Name + count suffix, to the right of the icon box.
                // Pull rarity color every frame so dynamic rarities (Master/Expert disco shift,
                // Calamity BurnishedAuric/Cosmilite, etc.) animate. _color is fallback only.
                float textX = iconBoxRect.Right + NodeTextSpacing;
                string text = GetDisplayText();
                Vector2 textPosition = GetCenteredBorderStringPosition(text, textX, centerY, _scale);
                Color drawColor = UIItemRenderingHelper.GetItemNameColor(_itemId, _color);
                Utils.DrawBorderString(spriteBatch, text, textPosition, drawColor, _scale);

                float textWidth = FontAssets.MouseText.Value.MeasureString(text).X * _scale;
                float chipStartX = textX + textWidth + InlineBadgeSpacing;

                bool stationHovered = false;

                // Status chip (MISSING/OWNED) — drawn just to the right of the text,
                // before any station badges. CRAFTABLE is suppressed via BuildStatusChip.
                if (_statusChip.IsVisible)
                {
                    Rectangle chipRect = DrawStatusChip(spriteBatch, chipStartX, centerY);
                    chipStartX = chipRect.Right + BadgeSpacing;
                }

                // Reserve a slot on the far right for the owned-count label (intermediate craftable
                // nodes only). Stations must stop before this slot so they cannot overlap the count.
                // The reserved width accounts for the extra right inset used by the label so stations
                // (which use the smaller `RightPadding`) wrap before intruding on the label column.
                float ownedLabelWidth = GetOwnedLabelWidth(out string ownedLabel);
                float rightReserve = ownedLabelWidth > 0f
                    ? ownedLabelWidth + OwnedLabelGap + (OwnedLabelRightPadding - RightPadding)
                    : 0f;

                if (_stations.Count > 0)
                    LayoutBadges(spriteBatch, x, y, dims.Width, chipStartX, rightReserve, out stationHovered);

                if (ownedLabelWidth > 0f)
                {
                    Vector2 ownedSize = FontAssets.MouseText.Value.MeasureString(ownedLabel) * OwnedLabelScale;
                    // Anchor the right edge at `OwnedLabelRightPadding` so it aligns with the
                    // leaf ingredient count's right edge (`UIIngredientRow.RightPadding = 10f`).
                    float ownedX = dims.X + dims.Width - OwnedLabelRightPadding - ownedSize.X;
                    float ownedY = centerY - ownedSize.Y * 0.5f;
                    Utils.DrawBorderString(spriteBatch, ownedLabel,
                        new Vector2(ownedX, ownedY), UIPalette.TreeOwnedCount, OwnedLabelScale);
                }

                var rect = dims.ToRectangle();
                if (!stationHovered &&
                    rect.Contains(Main.mouseX, Main.mouseY) &&
                    UIItemRenderingHelper.TryCreateDisplayItem(_itemId, out Item hoverItem))
                {
                    Main.HoverItem = hoverItem.Clone();
                    Main.hoverItemName = hoverItem.Name;
                }
            }

            private float GetOwnedLabelWidth(out string ownedLabel)
            {
                ownedLabel = string.Empty;

                if (!_showOwnedLabel || _haveLookup == null)
                    return 0f;

                int owned = Math.Max(0, _haveLookup(_itemId));
                ownedLabel = owned.ToString(CultureInfo.InvariantCulture);
                return FontAssets.MouseText.Value.MeasureString(ownedLabel).X * OwnedLabelScale;
            }

            private Rectangle DrawStatusChip(SpriteBatch spriteBatch, float leftX, float centerY)
            {
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(_statusChip.Text) * ChipScale;
                int chipWidth = (int)Math.Ceiling(textSize.X + ChipHorizontalPadding * 2f);
                int chipTop = (int)(centerY - ChipHeight * 0.5f);
                var chipRect = new Rectangle((int)leftX, chipTop, chipWidth, (int)ChipHeight);

                UIDrawHelper.DrawRect(spriteBatch, chipRect, _statusChip.Background);
                UIDrawHelper.DrawBorder(spriteBatch, chipRect, _statusChip.Border, 1);

                Vector2 textPos = new(
                    chipRect.X + (chipRect.Width - textSize.X) * 0.5f,
                    chipRect.Y + (chipRect.Height - textSize.Y) * 0.5f);
                Utils.DrawBorderString(spriteBatch, _statusChip.Text, textPos, _statusChip.TextColor, ChipScale);
                return chipRect;
            }

            private float CalculateDesiredHeight(float width)
            {
                if (_stations.Count == 0)
                    return BaseRowHeight;

                float textX = GetTextOriginX(0f);
                string text = GetDisplayText();
                float textWidth = FontAssets.MouseText.Value.MeasureString(text).X * _scale;
                float rowStart = textX + textWidth + InlineBadgeSpacing;

                if (_statusChip.IsVisible)
                {
                    Vector2 chipSize = FontAssets.MouseText.Value.MeasureString(_statusChip.Text) * ChipScale;
                    rowStart += chipSize.X + ChipHorizontalPadding * 2f + BadgeSpacing;
                }

                float ownedLabelWidth = GetOwnedLabelWidth(out _);
                float rightReserve = ownedLabelWidth > 0f
                    ? ownedLabelWidth + OwnedLabelGap + (OwnedLabelRightPadding - RightPadding)
                    : 0f;

                return LayoutBadges(null, 0f, 0f, width, rowStart, rightReserve, out _);
            }

            private float LayoutBadges(SpriteBatch spriteBatch, float x, float y, float width, float startX, float rightReserve, out bool hoveredAny)
            {
                hoveredAny = false;

                if (_stations.Count == 0)
                    return BaseRowHeight;

                float wrappedRowStartX = GetTextOriginX(x);
                float contentRight = x + width - RightPadding - rightReserve;
                float currentX = startX;
                float rowStartX = startX;
                float centerY = y + BaseRowHeight * 0.5f;
                float currentY = centerY - BadgeSize * 0.5f;
                bool placedAnyBadge = false;

                foreach (StationDisplayInfo station in _stations)
                {
                    float badgeWidth = GetBadgeWidth(station);
                    if (currentX + badgeWidth > contentRight)
                    {
                        if (!placedAnyBadge)
                        {
                            rowStartX = wrappedRowStartX;
                            currentX = rowStartX;
                            currentY = y + BaseRowHeight + RowSpacing;
                        }
                        else if (currentX > rowStartX)
                        {
                            currentX = rowStartX;
                            currentY += BadgeSize + RowSpacing;
                        }
                    }

                    Rectangle badgeRect = new(
                        SnapToPixel(currentX),
                        SnapToPixel(currentY),
                        (int)Math.Ceiling(badgeWidth),
                        (int)BadgeSize);

                    if (spriteBatch != null)
                    {
                        bool hovered = badgeRect.Contains(Main.mouseX, Main.mouseY);
                        DrawStationBadge(spriteBatch, station, badgeRect, hovered);
                        if (hovered)
                        {
                            hoveredAny = true;
                            ApplyHover(station);
                        }
                    }

                    currentX += badgeWidth + BadgeSpacing;
                    placedAnyBadge = true;
                }

                float badgeBottom = placedAnyBadge ? currentY + BadgeSize - y : BaseRowHeight;
                return Math.Max(BaseRowHeight, badgeBottom);
            }

            private string GetDisplayText()
            {
                return UIItemRenderingHelper.GetDisplayNameOrFallback(_itemId) + _suffix;
            }

            private float GetContentX(float x)
            {
                // Root sits flush-left. Children step by DepthIndent per level so the
                // root→child jump matches every subsequent parent→child jump.
                // contentX marks the arrow column's left edge; iconLeft sits one column to the right.
                return _depth < 0 ? x : x + (_depth + 1) * DepthIndent - ArrowColumnWidth;
            }

            private float GetIconBoxLeft(float x)
            {
                // Root has no arrow column; its icon sits at contentX directly.
                return _depth < 0 ? x : x + (_depth + 1) * DepthIndent;
            }

            private float GetTextOriginX(float x)
            {
                return GetIconBoxLeft(x) + IconBoxSize + NodeTextSpacing;
            }

            private static void DrawTriangle(SpriteBatch spriteBatch, Vector2 center, TriangleState state, Color color)
            {
                var pixel = TextureAssets.MagicPixel.Value;
                float half = TriangleSize / 2f;

                if (state == TriangleState.Expanded)
                {
                    // Downward triangle (▼): draw horizontal lines from top to bottom, narrowing
                    int rows = (int)TriangleSize;
                    for (int row = 0; row < rows; row++)
                    {
                        float progress = (float)row / (rows - 1);
                        float width = TriangleSize * (1f - progress);
                        float lx = center.X - width / 2f;
                        float ly = center.Y - half + row;
                        spriteBatch.Draw(pixel,
                            new Rectangle((int)lx, (int)ly, (int)Math.Max(1, width), 1),
                            color);
                    }
                }
                else if (state == TriangleState.Collapsed)
                {
                    // Right triangle (▶): draw vertical lines from left to right, narrowing
                    int cols = (int)TriangleSize;
                    for (int col = 0; col < cols; col++)
                    {
                        float progress = (float)col / (cols - 1);
                        float height = TriangleSize * (1f - progress);
                        float lx = center.X - half + col;
                        float ly = center.Y - height / 2f;
                        spriteBatch.Draw(pixel,
                            new Rectangle((int)lx, (int)ly, 1, (int)Math.Max(1, height)),
                            color);
                    }
                }
            }

            private static float GetBadgeWidth(StationDisplayInfo station)
            {
                if (station.HasDisplayItem)
                    return BadgeSize;

                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(station.DisplayName) * FallbackScale;
                return Math.Min(MaxFallbackBadgeWidth, textSize.X + 18f);
            }

            private static void DrawStationBadge(SpriteBatch spriteBatch, StationDisplayInfo station, Rectangle badgeRect, bool hovered)
            {
                UIDrawHelper.DrawRect(spriteBatch, badgeRect, hovered ? BadgeHoverColor : UIPalette.StationBg);
                UIDrawHelper.DrawBorder(spriteBatch, badgeRect, UIPalette.StationBorder, 1);

                if (station.HasDisplayItem)
                {
                    UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, station.ItemId, badgeRect.Center.ToVector2(), 18f);
                    return;
                }

                string text = TruncateTextToWidth(station.DisplayName, badgeRect.Width - 12f, FallbackScale);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * FallbackScale;
                Vector2 textPosition = new(
                    badgeRect.X + (badgeRect.Width - textSize.X) * 0.5f,
                    badgeRect.Y + (badgeRect.Height - textSize.Y) * 0.5f);
                Utils.DrawBorderString(spriteBatch, text, textPosition, UIPalette.StationText, FallbackScale);
            }

            private static void ApplyHover(StationDisplayInfo station)
            {
                if (station.HasDisplayItem &&
                    UIItemRenderingHelper.TryCreateDisplayItem(station.ItemId, out Item hoverItem))
                {
                    Main.HoverItem = hoverItem.Clone();
                }

                Main.hoverItemName = station.DisplayName;
            }

            private static string TruncateTextToWidth(string text, float maxWidth, float scale)
            {
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
                if (size.X <= maxWidth)
                    return text;

                string truncated = text;
                while (truncated.Length > 1)
                {
                    truncated = truncated[..^1];
                    string candidate = truncated + "...";
                    if (FontAssets.MouseText.Value.MeasureString(candidate).X * scale <= maxWidth)
                        return candidate;
                }

                return text;
            }
        }

        /// <summary>
        /// Text-only tree line (condition/meta) aligned with the tree node name column.
        /// Carries connector through-verticals only — never originates a branch stub.
        /// </summary>
        private class UITreeTextLine : UIElement, IConnectorTarget
        {
            private readonly string _text;
            private readonly Color _color;
            private readonly float _scale;
            private readonly int _depth;
            private ConnectorInfo _connector;

            public UITreeTextLine(string text, Color color, float scale, int depth)
            {
                _text = text;
                _color = color;
                _scale = scale;
                _depth = depth;
            }

            public void SetConnectorInfo(ConnectorInfo info)
            {
                _connector = info;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float centerY = dims.Y + dims.Height / 2f;

                // Connector throughs only — DrawBranchStub is false on condition lines so the
                // iconCenterY value is unused for stub geometry; pass row centerY for safety.
                UIRecipeTree.DrawConnectors(spriteBatch, dims, _connector, centerY);

                float iconBoxLeft = _depth < 0
                    ? dims.X
                    : dims.X + (_depth + 1) * DepthIndent;
                float textX = iconBoxLeft + IconBoxSize + NodeTextSpacing;
                Vector2 textPosition = GetCenteredBorderStringPosition(_text, textX, centerY, _scale);
                Utils.DrawBorderString(spriteBatch, _text, textPosition, _color, _scale);
            }
        }

    }
}
