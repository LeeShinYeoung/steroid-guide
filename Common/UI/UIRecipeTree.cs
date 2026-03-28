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

            // Root item title with icon
            AddItemLine(root.ItemId, "", "", Color.Gold, 0.85f);

            // Tree nodes
            AddChildren(root, 0);

            // Crafting station for root recipe
            if (root.UsedRecipe != null)
                AddCraftingStationLine(root.UsedRecipe, 0);

            // Alternative recipe button for root
            if (root.AlternativeRecipes != null && root.AlternativeRecipes.Count > 0)
            {
                var capturedRoot = root;
                AddClickableLine("Alternative Recipe \u25B6", new Color(150, 200, 255), 0.7f,
                    () => SwapAlternativeRecipe(capturedRoot));
            }
        }

        private void AddChildren(RecipeTreeNode node, int depth)
        {
            if (node.Children == null || node.Children.Count == 0)
                return;

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                bool isLast = i == node.Children.Count - 1;

                string indent = BuildIndent(depth);
                string connector = isLast ? "\u2514\u2500 " : "\u251C\u2500 ";

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

                string collapsePrefix = "";
                if (hasCraftableChildren)
                    collapsePrefix = isCollapsed ? "[+] " : "[-] ";

                string prefix = $"{indent}{connector}{collapsePrefix}";
                string suffix = $"{countStr}{statusStr}";

                if (hasCraftableChildren)
                {
                    var capturedChild = child;
                    AddClickableItemLine(child.ItemId, prefix, suffix, color, 0.7f,
                        () => ToggleCollapse(capturedChild.ItemId));
                }
                else
                {
                    AddItemLine(child.ItemId, prefix, suffix, color, 0.7f);
                }

                // Show children if not collapsed
                if (hasCraftableChildren && !isCollapsed)
                {
                    AddChildren(child, depth + 1);

                    if (child.UsedRecipe != null)
                        AddCraftingStationLine(child.UsedRecipe, depth + 1);

                    // Alternative Recipe button
                    if (child.AlternativeRecipes != null && child.AlternativeRecipes.Count > 0)
                    {
                        string altIndent = BuildIndent(depth + 1);
                        var capturedChild = child;
                        AddClickableLine($"{altIndent}   Alternative Recipe \u25B6", new Color(150, 200, 255), 0.65f,
                            () => SwapAlternativeRecipe(capturedChild));
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

        private void AddCraftingStationLine(Recipe recipe, int depth)
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
                string indent = BuildIndent(depth);
                string text = $"{indent}   Crafting Station: {string.Join(", ", tiles)}";
                AddLine(text, Color.LightBlue, 0.65f);
            }

            // Show conditions if any
            if (recipe.Conditions != null && recipe.Conditions.Count > 0)
            {
                string indent = BuildIndent(depth);
                var condNames = new List<string>();
                foreach (var cond in recipe.Conditions)
                {
                    condNames.Add(cond.Description.Value);
                }
                string text = $"{indent}   Conditions: {string.Join(", ", condNames)}";
                AddLine(text, new Color(180, 180, 220), 0.65f);
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

        private static string BuildIndent(int depth)
        {
            return depth > 0 ? new string(' ', depth * 3) : "";
        }

        private void AddLine(string text, Color color, float scale)
        {
            var line = new UIText(text, scale);
            line.TextColor = color;
            line.Width.Set(0f, 1f);
            line.Height.Set(20f, 0f);
            _list.Add(line);
        }

        private void AddClickableLine(string text, Color color, float scale, Action onClick)
        {
            var line = new UIText(text, scale);
            line.TextColor = color;
            line.Width.Set(0f, 1f);
            line.Height.Set(20f, 0f);
            line.OnLeftClick += (evt, el) => onClick();
            _list.Add(line);
        }

        private void AddItemLine(int itemId, string prefix, string suffix, Color color, float scale)
        {
            var line = new UITreeItemLine(itemId, prefix, suffix, color, scale);
            line.Width.Set(0f, 1f);
            line.Height.Set(24f, 0f);
            _list.Add(line);
        }

        private void AddClickableItemLine(int itemId, string prefix, string suffix, Color color, float scale, Action onClick)
        {
            var line = new UITreeItemLine(itemId, prefix, suffix, color, scale);
            line.Width.Set(0f, 1f);
            line.Height.Set(24f, 0f);
            line.OnLeftClick += (evt, el) => onClick();
            _list.Add(line);
        }

        private void ShowPlaceholder()
        {
            _list?.Clear();
            AddLine("Click an item above to view its recipe tree.", Color.Gray, 0.75f);
        }

        private class UITreeItemLine : UIElement
        {
            private readonly int _itemId;
            private readonly string _prefix;
            private readonly string _suffix;
            private readonly Color _color;
            private readonly float _scale;

            public UITreeItemLine(int itemId, string prefix, string suffix, Color color, float scale)
            {
                _itemId = itemId;
                _prefix = prefix;
                _suffix = suffix;
                _color = color;
                _scale = scale;
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var dims = GetDimensions();
                float x = dims.X;
                float y = dims.Y;
                float centerY = y + dims.Height / 2f;

                // Draw prefix text (indent + connector + collapse indicator)
                float prefixWidth = 0f;
                if (!string.IsNullOrEmpty(_prefix))
                {
                    var prefixSize = Utils.DrawBorderString(spriteBatch, _prefix,
                        new Vector2(x, centerY - 8f), Color.White, _scale);
                    prefixWidth = prefixSize.X;
                }

                // Draw item icon (20x20)
                const float iconSize = 20f;
                float iconX = x + prefixWidth + iconSize / 2f;
                DrawItemIcon(spriteBatch, _itemId, new Vector2(iconX, centerY), iconSize);

                // Draw item name + suffix
                float textX = x + prefixWidth + iconSize + 4f;
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
    }
}
