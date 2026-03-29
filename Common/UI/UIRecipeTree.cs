using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UIRecipeTree : UIElement
    {
        private UIList _list;
        private UIScrollbar _scrollbar;
        private RecipeTreeNode _currentRoot;
        private RecipeGraphData _currentGraph;
        private Dictionary<int, int> _currentAvailable;
        private readonly HashSet<int> _collapsedItemIds = new();

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
            AddChildren(root, 0, emptyParentLines);

            // Crafting station for root recipe
            if (root.UsedRecipe != null)
                AddCraftingStationLine(root.UsedRecipe, 0, emptyParentLines);

            // Alternative recipe button for root
            if (root.AlternativeRecipes != null && root.AlternativeRecipes.Count > 0)
            {
                var capturedRoot = root;
                AddTreeTextLine("Alternative Recipe \u25B6", new Color(150, 200, 255), 0.7f,
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

                bool hasCraftableChildren = child.Status == NodeStatus.Craftable
                    && child.Children != null && child.Children.Count > 0;
                bool isCollapsed = _collapsedItemIds.Contains(child.ItemId);

                string suffix = $"{countStr}{statusStr}";

                TriangleState triangleState = TriangleState.None;
                if (hasCraftableChildren)
                    triangleState = isCollapsed ? TriangleState.Collapsed : TriangleState.Expanded;

                var line = new UITreeItemLine(child.ItemId, suffix, color, 0.7f,
                    depth, isLast, parentLines, triangleState);
                line.Width.Set(0f, 1f);
                line.Height.Set(24f, 0f);

                if (hasCraftableChildren)
                {
                    var capturedChild = child;
                    line.OnLeftClick += (evt, el) => ToggleCollapse(capturedChild.ItemId);
                }

                _list.Add(line);

                // Show children if not collapsed
                if (hasCraftableChildren && !isCollapsed)
                {
                    var childParentLines = new List<bool>(parentLines) { !isLast };

                    AddChildren(child, depth + 1, childParentLines);

                    if (child.UsedRecipe != null)
                        AddCraftingStationLine(child.UsedRecipe, depth + 1, childParentLines);

                    // Alternative Recipe button
                    if (child.AlternativeRecipes != null && child.AlternativeRecipes.Count > 0)
                    {
                        var capturedChild = child;
                        AddTreeTextLine("Alternative Recipe \u25B6", new Color(150, 200, 255), 0.65f,
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
            if (node.AlternativeRecipes == null || node.AlternativeRecipes.Count == 0)
                return;

            // Rotate: current recipe goes to end of alternatives, first alternative becomes current
            var oldRecipe = node.UsedRecipe;
            node.UsedRecipe = node.AlternativeRecipes[0];
            node.AlternativeRecipes.RemoveAt(0);
            node.AlternativeRecipes.Add(oldRecipe);

            // Rebuild children for this node with the new recipe
            if (_currentGraph != null && _currentAvailable != null)
            {
                node.Children.Clear();
                int remaining = Math.Max(1, node.RequiredCount - node.OwnedCount);
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

                // Update node status based on new children
                bool canMake = true;
                foreach (var child in node.Children)
                {
                    if (child.Status == NodeStatus.Missing)
                    {
                        canMake = false;
                        break;
                    }
                }
                node.Status = canMake ? NodeStatus.Craftable : NodeStatus.Missing;
            }

            // Re-render tree
            SetTree(_currentRoot);
        }

        private void AddCraftingStationLine(Recipe recipe, int depth, List<bool> parentLines)
        {
            var tiles = new List<string>();
            foreach (int tileId in recipe.requiredTile)
            {
                if (tileId < 0) continue;

                string tileName = GetTileName(tileId);
                tiles.Add(tileName);
            }

            if (tiles.Count > 0)
            {
                string text = $"Crafting Station: {string.Join(", ", tiles)}";
                AddTreeTextLine(text, Color.LightBlue, 0.65f, depth, parentLines);
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

        private static string GetTileName(int tileId)
        {
            // Try modded tile first
            var modTile = Terraria.ModLoader.TileLoader.GetTile(tileId);
            if (modTile != null)
                return modTile.Name;

            // Vanilla: use map object name
            try
            {
                int lookup = Terraria.Map.MapHelper.TileToLookup(tileId, 0);
                string name = Lang.GetMapObjectName(lookup);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            catch
            {
                // Fallback
            }

            return $"Tile #{tileId}";
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

        private void ShowPlaceholder()
        {
            _list?.Clear();
            var line = new UIText("Click an item above to view its recipe tree.", 0.75f);
            line.TextColor = Color.Gray;
            line.Width.Set(0f, 1f);
            line.Height.Set(20f, 0f);
            _list?.Add(line);
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
                DrawItemIcon(spriteBatch, _itemId, new Vector2(iconX, centerY), iconSize);

                // Draw item name + suffix
                float textX = contentX + triangleWidth + iconSize + 4f;
                var item = new Item();
                item.SetDefaults(_itemId);
                string text = item.Name + _suffix;
                Utils.DrawBorderString(spriteBatch, text, new Vector2(textX, centerY - 8f), _color, _scale);

                // Hover tooltip
                var rect = dims.ToRectangle();
                if (rect.Contains(Main.mouseX, Main.mouseY))
                {
                    var hoverItem = new Item();
                    hoverItem.SetDefaults(_itemId);
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

            private static void DrawItemIcon(SpriteBatch spriteBatch, int itemId, Vector2 center, float maxDim)
            {
                Main.instance.LoadItem(itemId);
                var texture = TextureAssets.Item[itemId].Value;

                Rectangle frame;
                if (Main.itemAnimations[itemId] != null)
                    frame = Main.itemAnimations[itemId].GetFrame(texture);
                else
                    frame = texture.Frame();

                float scale = 1f;
                if (frame.Width > maxDim || frame.Height > maxDim)
                    scale = maxDim / Math.Max(frame.Width, frame.Height);

                spriteBatch.Draw(texture, center, frame, Color.White, 0f,
                    frame.Size() / 2f, scale, SpriteEffects.None, 0f);
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
    }
}
