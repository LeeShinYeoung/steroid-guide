using System;
using System.Collections.Generic;
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
    public class UIRecipeTree : UIElement
    {
        private const string EmptyStateText = "Click an item above to view its recipe tree.";
        private const string AlternativeRecipeText = "Alternative Recipe";
        private UIList _list;
        private UIScrollbar _scrollbar;
        private UIEmptyStatePlaceholder _placeholder;
        private RecipeTreeNode _currentRoot;
        private RecipeGraphData _currentGraph;
        private Dictionary<int, int> _currentAvailable;
        private readonly HashSet<int> _collapsedItemIds = new();
        private static readonly Dictionary<int, int> TileDisplayItemCache = new();

        private const float DepthIndent = 20f;
        private static readonly Color LineColor = Color.Gray * 0.6f;
        private const int LineThickness = 2;

        public override void OnInitialize()
        {
            var bg = new UIPanel();
            bg.Width.Set(0f, 1f);
            bg.Height.Set(0f, 1f);
            bg.SetPadding(6f);
            Append(bg);

            _scrollbar = new UIScrollbar();
            _scrollbar.Height.Set(-12f, 1f);
            _scrollbar.Top.Set(6f, 0f);
            _scrollbar.Left.Set(-22f, 1f);
            bg.Append(_scrollbar);

            _list = new UIList();
            _list.Width.Set(-28f, 1f);
            _list.Height.Set(0f, 1f);
            _list.ListPadding = 2f;
            _list.SetScrollbar(_scrollbar);
            bg.Append(_list);

            _placeholder = new UIEmptyStatePlaceholder(EmptyStateText, Color.Gray, 0.75f, () => _currentRoot == null);
            _placeholder.Width.Set(-28f, 1f);
            _placeholder.Height.Set(0f, 1f);
            bg.Append(_placeholder);

            ShowPlaceholder();
        }

        public void ClearTree()
        {
            _currentRoot = null;
            _list?.Clear();
            ShowPlaceholder();
        }

        public void SetTree(RecipeTreeNode root, RecipeGraphData graph = null, Dictionary<int, int> available = null)
        {
            _currentRoot = root;
            if (graph != null) _currentGraph = graph;
            if (available != null) _currentAvailable = available;

            _list.Clear();

            if (root == null)
            {
                ShowPlaceholder();
                return;
            }

            // Root item title with icon — no tree lines
            var rootLine = new UITreeItemLine(root.ItemId, "", Color.Gold, 0.85f, -1, false, null, TriangleState.None);
            rootLine.Width.Set(0f, 1f);
            rootLine.Height.Set(24f, 0f);
            _list.Add(rootLine);

            // Tree nodes
            var emptyParentLines = new List<bool>();

            // Crafting station for root recipe
            if (root.UsedRecipe != null)
                AddCraftingStationLine(root.UsedRecipe, 0, emptyParentLines);

            AddChildren(root, 0, emptyParentLines);

            // Alternative recipe button for root
            if (HasAlternativeRecipes(root))
            {
                var capturedRoot = root;
                AddAlternativeActionLine(AlternativeRecipeText, new Color(150, 200, 255), 0.7f,
                    0, emptyParentLines, () => SwapAlternativeRecipe(capturedRoot));
            }
        }

        private void AddChildren(RecipeTreeNode node, int depth, List<bool> parentLines)
        {
            if (node.Children == null || node.Children.Count == 0)
                return;

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                bool isLast = i == node.Children.Count - 1;

                string countStr = child.RequiredCount > 1 ? $" x{child.RequiredCount}" : "";
                string statusStr = child.Status switch
                {
                    NodeStatus.Owned => $" [owned: {child.OwnedCount}]",
                    NodeStatus.Craftable => " [craftable]",
                    _ => " [missing]"
                };

                Color color = child.Status switch
                {
                    NodeStatus.Owned => Color.LightGreen,
                    NodeStatus.Craftable => Color.Yellow,
                    _ => Color.IndianRed
                };

                bool hasRecipeDetails = HasRecipeDetails(child);
                bool hasDisplayableChildren = HasDisplayableRecipeChildren(child);
                bool isCollapsed = IsCollapsed(child);

                string suffix = $"{countStr}{statusStr}";

                TriangleState triangleState = TriangleState.None;
                if (hasDisplayableChildren)
                    triangleState = isCollapsed ? TriangleState.Collapsed : TriangleState.Expanded;

                var line = new UITreeItemLine(child.ItemId, suffix, color, 0.7f,
                    depth, isLast, parentLines, triangleState);
                line.Width.Set(0f, 1f);
                line.Height.Set(24f, 0f);

                if (hasDisplayableChildren)
                {
                    var capturedChild = child;
                    line.OnLeftClick += (evt, el) => ToggleCollapse(capturedChild.ItemId);
                }

                _list.Add(line);

                // Show children if not collapsed
                if (hasRecipeDetails && !isCollapsed)
                {
                    var childParentLines = new List<bool>(parentLines) { !isLast };

                    AddCraftingStationLine(child.UsedRecipe, depth + 1, childParentLines);

                    AddChildren(child, depth + 1, childParentLines);

                    if (HasAlternativeRecipes(child))
                    {
                        var capturedChild = child;
                        AddAlternativeActionLine(AlternativeRecipeText, new Color(150, 200, 255), 0.65f,
                            depth + 1, childParentLines, () => SwapAlternativeRecipe(capturedChild));
                    }
                }
            }
        }

        private void ToggleCollapse(int itemId)
        {
            if (!_collapsedItemIds.Remove(itemId))
                _collapsedItemIds.Add(itemId);

            if (_currentRoot != null)
                SetTree(_currentRoot);
        }

        private void SwapAlternativeRecipe(RecipeTreeNode node)
        {
            if (!HasAlternativeRecipes(node))
                return;

            // Rotate: current recipe goes to end of alternatives, first alternative becomes current
            var oldRecipe = node.UsedRecipe;
            node.UsedRecipe = node.AlternativeRecipes[0];
            node.AlternativeRecipes.RemoveAt(0);
            if (oldRecipe != null)
                node.AlternativeRecipes.Add(oldRecipe);

            // Rebuild children for this node with the new recipe
            if (node.UsedRecipe != null && _currentGraph != null && _currentAvailable != null)
            {
                node.Children ??= new List<RecipeTreeNode>();
                node.Children.Clear();
                int usableOwnedCount = node.IgnoreOwnedForCraftability ? 0 : node.OwnedCount;
                int remaining = Math.Max(1, node.RequiredCount - usableOwnedCount);
                int batchSize = Math.Max(1, node.UsedRecipe.createItem.stack);
                int batches = (remaining + batchSize - 1) / batchSize;

                foreach (var ingredient in node.UsedRecipe.requiredItem)
                {
                    if (ingredient.type <= ItemID.None)
                        continue;
                    int ingredientNeeded = ingredient.stack * batches;
                    node.Children.Add(RecipeAnalyzer.BuildRecipeTree(
                        ingredient.type, ingredientNeeded, _currentGraph, _currentAvailable));
                }

                node.Status = GetStatusForSelectedRecipe(node.Children);
            }

            // Re-render tree
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
            return HasDisplayableRecipeChildren(node) && _collapsedItemIds.Contains(node.ItemId);
        }

        private static bool HasAlternativeRecipes(RecipeTreeNode node)
        {
            return node?.AlternativeRecipes != null && node.AlternativeRecipes.Count > 0;
        }

        private static NodeStatus GetStatusForSelectedRecipe(List<RecipeTreeNode> children)
        {
            if (children == null)
                return NodeStatus.Missing;

            foreach (var child in children)
            {
                if (child.Status == NodeStatus.Missing)
                    return NodeStatus.Missing;
            }

            return NodeStatus.Craftable;
        }

        private void AddCraftingStationLine(Recipe recipe, int depth, List<bool> parentLines)
        {
            List<StationDisplayInfo> stations = ResolveStations(recipe);
            if (stations.Count > 0)
            {
                var line = new UITreeStationLine(stations, depth, parentLines);
                line.Width.Set(0f, 1f);
                line.Height.Set(34f, 0f);
                _list.Add(line);
            }

            // Show conditions if any
            if (recipe.Conditions != null && recipe.Conditions.Count > 0)
            {
                var condNames = new List<string>();
                foreach (var cond in recipe.Conditions)
                {
                    condNames.Add(cond.Description.Value);
                }
                string text = $"Conditions: {string.Join(", ", condNames)}";
                AddTreeTextLine(text, new Color(180, 180, 220), 0.65f, depth, parentLines);
            }
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

        private void AddTreeTextLine(string text, Color color, float scale, int depth, List<bool> parentLines, Action onClick = null)
        {
            var line = new UITreeTextLine(text, color, scale, depth, parentLines);
            line.Width.Set(0f, 1f);
            line.Height.Set(20f, 0f);
            if (onClick != null)
                line.OnLeftClick += (evt, el) => onClick();
            _list.Add(line);
        }

        private void AddAlternativeActionLine(string text, Color color, float scale, int depth, List<bool> parentLines, Action onClick)
        {
            var line = new UITreeActionLine(text, color, scale, depth, parentLines);
            line.Width.Set(0f, 1f);
            line.Height.Set(24f, 0f);
            line.OnLeftClick += (evt, el) => onClick();
            _list.Add(line);
        }

        private void ShowPlaceholder()
        {
            _list?.Clear();
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

        private enum TriangleState
        {
            None,
            Expanded,
            Collapsed
        }

        /// <summary>
        /// Tree node line with item icon and graphic connector lines.
        /// </summary>
        private class UITreeItemLine : UIElement
        {
            private readonly int _itemId;
            private readonly string _suffix;
            private readonly Color _color;
            private readonly float _scale;
            private readonly int _depth;
            private readonly bool _isLast;
            private readonly List<bool> _parentLines;
            private readonly TriangleState _triangleState;

            private const float TriangleSize = 8f;

            public UITreeItemLine(int itemId, string suffix, Color color, float scale,
                int depth, bool isLast, List<bool> parentLines, TriangleState triangleState)
            {
                _itemId = itemId;
                _suffix = suffix;
                _color = color;
                _scale = scale;
                _depth = depth;
                _isLast = isLast;
                _parentLines = parentLines != null ? new List<bool>(parentLines) : null;
                _triangleState = triangleState;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float x = dims.X;
                float y = dims.Y;
                float centerY = y + dims.Height / 2f;

                // Draw graphic tree lines
                if (_depth >= 0)
                    DrawTreeLines(spriteBatch, x, y, dims.Height, centerY);

                // Content area starts after tree connector region
                float contentX = _depth >= 0 ? x + (_depth + 1) * DepthIndent : x;

                // Draw triangle toggle for collapsible nodes
                float triangleWidth = 0f;
                if (_triangleState != TriangleState.None)
                {
                    bool hovered = GetDimensions().ToRectangle().Contains(Main.mouseX, Main.mouseY);
                    Color triColor = hovered ? Color.Gold : Color.White;
                    DrawTriangle(spriteBatch, new Vector2(contentX, centerY), _triangleState, triColor);
                    triangleWidth = TriangleSize + 4f;
                }

                // Draw item icon (20x20)
                const float iconSize = 20f;
                float iconX = contentX + triangleWidth + iconSize / 2f;
                UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, _itemId, new Vector2(iconX, centerY), iconSize);

                // Draw item name + suffix
                float textX = contentX + triangleWidth + iconSize + 4f;
                string text = UIItemRenderingHelper.GetDisplayNameOrFallback(_itemId) + _suffix;
                Utils.DrawBorderString(spriteBatch, text, new Vector2(textX, centerY - 8f), _color, _scale);

                // Hover tooltip
                var rect = dims.ToRectangle();
                if (rect.Contains(Main.mouseX, Main.mouseY) &&
                    UIItemRenderingHelper.TryCreateDisplayItem(_itemId, out Item hoverItem))
                {
                    Main.HoverItem = hoverItem.Clone();
                    Main.hoverItemName = hoverItem.Name;
                }
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

            private void DrawTreeLines(SpriteBatch spriteBatch, float x, float y, float height, float centerY)
            {
                var pixel = TextureAssets.MagicPixel.Value;

                // Continuation vertical lines for ancestors
                if (_parentLines != null)
                {
                    for (int d = 0; d < _parentLines.Count; d++)
                    {
                        if (_parentLines[d])
                        {
                            float lineX = x + d * DepthIndent + DepthIndent / 2f;
                            spriteBatch.Draw(pixel,
                                new Rectangle((int)(lineX - LineThickness / 2f), (int)y,
                                    LineThickness, (int)height),
                                LineColor);
                        }
                    }
                }

                // Connector at current depth
                float connX = x + _depth * DepthIndent + DepthIndent / 2f;

                // Vertical part: top to center (last child, L-shape) or top to bottom
                float vertBottom = _isLast ? centerY : y + height;
                spriteBatch.Draw(pixel,
                    new Rectangle((int)(connX - LineThickness / 2f), (int)y,
                        LineThickness, (int)(vertBottom - y)),
                    LineColor);

                // Horizontal part: from connector to content area
                float horzRight = x + (_depth + 1) * DepthIndent;
                spriteBatch.Draw(pixel,
                    new Rectangle((int)connX, (int)(centerY - LineThickness / 2f),
                        (int)(horzRight - connX), LineThickness),
                    LineColor);
            }
        }

        private class UITreeStationLine : UIElement
        {
            private const float FallbackScale = 0.58f;
            private const float HorizontalPadding = 4f;
            private const float VerticalPadding = 4f;
            private const float BadgeSize = 24f;
            private const float BadgeSpacing = 6f;
            private const float RowSpacing = 4f;
            private const float MaxFallbackBadgeWidth = 140f;

            private static readonly Color BadgeBackgroundColor = new(40, 56, 88, 210);
            private static readonly Color BadgeBorderColor = new(124, 166, 214, 200);
            private static readonly Color BadgeHoverColor = new(82, 112, 154, 220);
            private static readonly Color FallbackTextColor = new(225, 235, 248);

            private readonly int _depth;
            private readonly List<bool> _parentLines;
            private readonly List<StationDisplayInfo> _stations;

            public UITreeStationLine(List<StationDisplayInfo> stations, int depth, List<bool> parentLines)
            {
                _stations = stations ?? new List<StationDisplayInfo>();
                _depth = depth;
                _parentLines = parentLines != null ? new List<bool>(parentLines) : null;
            }

            public override void Recalculate()
            {
                base.Recalculate();

                float desiredHeight = CalculateLayoutHeight(GetDimensions().Width);
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

                DrawAncestorLines(spriteBatch, x, y, dims.Height);

                float contentX = x + (_depth + 1) * DepthIndent + HorizontalPadding;
                float contentRight = dims.X + dims.Width - HorizontalPadding;
                float currentX = contentX;
                float currentY = y + VerticalPadding;
                float rowStartX = contentX;
                float rowHeight = BadgeSize;

                foreach (StationDisplayInfo station in _stations)
                {
                    float badgeWidth = GetBadgeWidth(station);
                    float maxRight = contentRight;
                    if (currentX + badgeWidth > maxRight && currentX > rowStartX)
                    {
                        currentX = rowStartX;
                        currentY += rowHeight + RowSpacing;
                    }

                    Rectangle badgeRect = new(
                        (int)currentX,
                        (int)currentY,
                        (int)Math.Ceiling(badgeWidth),
                        (int)BadgeSize);
                    bool hovered = badgeRect.Contains(Main.mouseX, Main.mouseY);

                    DrawStationBadge(spriteBatch, station, badgeRect, hovered);

                    if (hovered)
                        ApplyHover(station);

                    currentX += badgeWidth + BadgeSpacing;
                    rowStartX = contentX;
                }
            }

            private float CalculateLayoutHeight(float width)
            {
                if (_stations.Count == 0)
                    return BadgeSize + VerticalPadding * 2f;

                float contentWidth = Math.Max(0f, width - (_depth + 1) * DepthIndent - HorizontalPadding * 2f);
                float availableWidth = Math.Max(BadgeSize, contentWidth);
                float currentX = 0f;
                float rowStartX = 0f;
                int rows = 1;
                foreach (StationDisplayInfo station in _stations)
                {
                    float badgeWidth = GetBadgeWidth(station);
                    if (currentX + badgeWidth > availableWidth && currentX > rowStartX)
                    {
                        rows++;
                        currentX = rowStartX;
                    }

                    currentX += badgeWidth + BadgeSpacing;
                    rowStartX = 0f;
                }

                return VerticalPadding * 2f + rows * BadgeSize + (rows - 1) * RowSpacing;
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
                DrawFramedBox(
                    spriteBatch,
                    badgeRect,
                    hovered ? BadgeHoverColor : BadgeBackgroundColor,
                    BadgeBorderColor);

                if (station.HasDisplayItem)
                {
                    UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, station.ItemId, badgeRect.Center.ToVector2(), 18f);
                    return;
                }

                string text = TruncateTextToWidth(station.DisplayName, badgeRect.Width - 12f, FallbackScale);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * FallbackScale;
                Vector2 textPosition = new(
                    badgeRect.X + (badgeRect.Width - textSize.X) * 0.5f,
                    badgeRect.Y + (badgeRect.Height - textSize.Y) * 0.5f - 1f);
                Utils.DrawBorderString(spriteBatch, text, textPosition, FallbackTextColor, FallbackScale);
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

            private void DrawAncestorLines(SpriteBatch spriteBatch, float x, float y, float height)
            {
                if (_parentLines == null)
                    return;

                var pixel = TextureAssets.MagicPixel.Value;
                for (int d = 0; d < _parentLines.Count; d++)
                {
                    if (_parentLines[d])
                    {
                        float lineX = x + d * DepthIndent + DepthIndent / 2f;
                        spriteBatch.Draw(
                            pixel,
                            new Rectangle((int)(lineX - LineThickness / 2f), (int)y, LineThickness, (int)height),
                            LineColor);
                    }
                }
            }

            private static void DrawFramedBox(SpriteBatch spriteBatch, Rectangle rect, Color backgroundColor, Color borderColor)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                spriteBatch.Draw(pixel, rect, backgroundColor);

                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor);
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
        /// Text-only tree line that draws ancestor continuation lines with pixel-based indentation.
        /// Used for crafting station info, conditions, and alternative recipe buttons.
        /// </summary>
        private class UITreeTextLine : UIElement
        {
            private readonly string _text;
            private readonly Color _color;
            private readonly float _scale;
            private readonly int _depth;
            private readonly List<bool> _parentLines;

            public UITreeTextLine(string text, Color color, float scale, int depth, List<bool> parentLines)
            {
                _text = text;
                _color = color;
                _scale = scale;
                _depth = depth;
                _parentLines = parentLines != null ? new List<bool>(parentLines) : null;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float x = dims.X;
                float y = dims.Y;
                float centerY = y + dims.Height / 2f;

                // Draw continuation vertical lines for ancestors
                if (_parentLines != null)
                {
                    var pixel = TextureAssets.MagicPixel.Value;
                    for (int d = 0; d < _parentLines.Count; d++)
                    {
                        if (_parentLines[d])
                        {
                            float lineX = x + d * DepthIndent + DepthIndent / 2f;
                            spriteBatch.Draw(pixel,
                                new Rectangle((int)(lineX - LineThickness / 2f), (int)y,
                                    LineThickness, (int)dims.Height),
                                LineColor);
                        }
                    }
                }

                // Content starts after tree area
                float contentX = x + (_depth + 1) * DepthIndent;
                Utils.DrawBorderString(spriteBatch, _text, new Vector2(contentX, centerY - 8f), _color, _scale);
            }
        }

        private class UITreeActionLine : UIElement
        {
            private const float HorizontalPadding = 8f;
            private const float VerticalPadding = 3f;
            private const float ArrowWidth = 10f;
            private const float ArrowHeight = 8f;
            private const float ArrowSpacing = 8f;

            private static readonly Color BackgroundColor = new(48, 72, 108, 205);
            private static readonly Color BorderColor = new(112, 158, 212, 225);
            private static readonly Color HoverBackgroundColor = new(66, 96, 138, 225);
            private static readonly Color HoverBorderColor = new(182, 212, 248, 235);

            private readonly string _text;
            private readonly Color _color;
            private readonly float _scale;
            private readonly int _depth;
            private readonly List<bool> _parentLines;

            public UITreeActionLine(string text, Color color, float scale, int depth, List<bool> parentLines)
            {
                _text = text;
                _color = color;
                _scale = scale;
                _depth = depth;
                _parentLines = parentLines != null ? new List<bool>(parentLines) : null;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float x = dims.X;
                float y = dims.Y;
                float centerY = y + dims.Height / 2f;

                DrawAncestorLines(spriteBatch, x, y, dims.Height);

                float contentX = x + (_depth + 1) * DepthIndent;
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(_text) * _scale;
                float buttonWidth = textSize.X + HorizontalPadding * 2f + ArrowSpacing + ArrowWidth;
                float buttonHeight = textSize.Y + VerticalPadding * 2f + 2f;
                Rectangle buttonRect = new(
                    (int)contentX,
                    (int)(centerY - buttonHeight / 2f),
                    (int)Math.Ceiling(buttonWidth),
                    (int)Math.Ceiling(buttonHeight));

                Color backgroundColor = IsMouseHovering ? HoverBackgroundColor : BackgroundColor;
                Color borderColor = IsMouseHovering ? HoverBorderColor : BorderColor;
                DrawFramedBox(spriteBatch, buttonRect, backgroundColor, borderColor);

                Vector2 textPosition = new(
                    buttonRect.X + HorizontalPadding,
                    centerY - textSize.Y / 2f - 1f);
                Utils.DrawBorderString(spriteBatch, _text, textPosition, _color, _scale);

                Vector2 arrowCenter = new(
                    buttonRect.Right - HorizontalPadding - ArrowWidth / 2f,
                    centerY);
                DrawChevron(spriteBatch, arrowCenter, _color);
            }

            private void DrawAncestorLines(SpriteBatch spriteBatch, float x, float y, float height)
            {
                if (_parentLines == null)
                    return;

                var pixel = TextureAssets.MagicPixel.Value;
                for (int d = 0; d < _parentLines.Count; d++)
                {
                    if (_parentLines[d])
                    {
                        float lineX = x + d * DepthIndent + DepthIndent / 2f;
                        spriteBatch.Draw(pixel,
                            new Rectangle((int)(lineX - LineThickness / 2f), (int)y,
                                LineThickness, (int)height),
                            LineColor);
                    }
                }
            }

            private static void DrawChevron(SpriteBatch spriteBatch, Vector2 center, Color color)
            {
                Vector2 leftTop = new(center.X - ArrowWidth / 2f, center.Y - ArrowHeight / 2f);
                Vector2 right = new(center.X + ArrowWidth / 2f, center.Y);
                Vector2 leftBottom = new(center.X - ArrowWidth / 2f, center.Y + ArrowHeight / 2f);

                DrawLine(spriteBatch, leftTop, right, color, 2f);
                DrawLine(spriteBatch, right, leftBottom, color, 2f);
            }

            private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
            {
                Vector2 edge = end - start;
                if (edge.LengthSquared() <= 0f)
                    return;

                float angle = MathF.Atan2(edge.Y, edge.X);
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    start,
                    null,
                    color,
                    angle,
                    new Vector2(0f, 0.5f),
                    new Vector2(edge.Length(), thickness),
                    SpriteEffects.None,
                    0f);
            }

            private static void DrawFramedBox(SpriteBatch spriteBatch, Rectangle rect, Color backgroundColor, Color borderColor)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                spriteBatch.Draw(pixel, rect, backgroundColor);

                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), borderColor);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), borderColor);
            }
        }
    }
}
