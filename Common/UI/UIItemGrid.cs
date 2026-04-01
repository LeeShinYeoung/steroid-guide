using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UIItemGrid : UIElement
    {
        private readonly struct GridLayout
        {
            public GridLayout(float cellWidth, float cellHeight)
            {
                CellWidth = cellWidth;
                CellHeight = cellHeight;
            }

            public float CellWidth { get; }
            public float CellHeight { get; }
        }

        private List<int> _items = new();
        private int _selectedItemId = -1;
        private const int TargetColumns = 12;
        private const int TargetRows = 3;
        private const float BaseCellWidth = 48f;
        private const float BaseCellHeight = 60f;
        private const float CellPadding = 6f;
        private const float CellAspectRatio = BaseCellHeight / BaseCellWidth;
        private const float IconCenterYRatio = 20f / BaseCellHeight;
        private const float NameTopRatio = 38f / BaseCellHeight;
        private string _emptyStateText = "No craftable items found.";

        public event Action<int> OnItemSelected;
        public event Action<int> OnPageScrollRequested;

        public int Columns => TargetColumns;

        public int Rows => TargetRows;

        public int ItemsPerPage => Columns * Rows;

        public static float GetPreferredHeight(float availableWidth)
        {
            float cellWidth = GetCellWidth(availableWidth);
            float cellHeight = cellWidth * CellAspectRatio;
            return TargetRows * cellHeight + (TargetRows - 1) * CellPadding;
        }

        public void SetItems(List<int> items, int selectedId)
        {
            _items = items ?? new List<int>();
            _selectedItemId = selectedId;
        }

        public void SetEmptyStateText(string emptyStateText)
        {
            _emptyStateText = emptyStateText ?? string.Empty;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            if (TryGetItemIndexAtPosition(evt.MousePosition, out int index))
            {
                OnItemSelected?.Invoke(_items[index]);
            }
        }

        public override void ScrollWheel(UIScrollWheelEvent evt)
        {
            if (!ContainsPoint(Main.MouseScreen))
            {
                base.ScrollWheel(evt);
                return;
            }

            int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
            if (scrollDelta != 0)
            {
                OnPageScrollRequested?.Invoke(scrollDelta);
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var dims = GetDimensions();
            float startX = dims.X;
            float startY = dims.Y;
            GridLayout layout = GetLayout(dims);

            for (int i = 0; i < _items.Count && i < ItemsPerPage; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                Rectangle cellRect = GetCellRectangle(startX, startY, layout, col, row);

                int itemId = _items[i];

                // Cell background
                Color bgColor = itemId == _selectedItemId
                    ? new Color(70, 70, 130, 220)
                    : new Color(35, 35, 60, 200);

                spriteBatch.Draw(TextureAssets.MagicPixel.Value, cellRect, bgColor);

                // Draw border for selected item
                if (itemId == _selectedItemId)
                {
                    DrawBorder(spriteBatch, cellRect, Color.Gold, 2);
                }

                // Draw item icon (centered horizontally, shifted up to leave room for name)
                float iconCenterY = cellRect.Y + cellRect.Height * IconCenterYRatio;
                DrawItemIcon(spriteBatch, itemId, new Vector2(cellRect.X + cellRect.Width * 0.5f, iconCenterY));

                // Draw item name below icon
                float nameY = cellRect.Y + cellRect.Height * NameTopRatio;
                DrawItemName(spriteBatch, itemId, cellRect.X, nameY, cellRect.Width);

                // Hover: tooltip + highlight
                if (cellRect.Contains(Main.mouseX, Main.mouseY))
                {
                    // Brighten the existing background color instead of using a white overlay
                    Color hoverBg = new Color(
                        Math.Min(bgColor.R + 30, 255),
                        Math.Min(bgColor.G + 30, 255),
                        Math.Min(bgColor.B + 30, 255),
                        bgColor.A);
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value, cellRect, hoverBg);

                    // Thin highlight border (1px, light gray) — distinct from the gold selected border
                    if (itemId != _selectedItemId)
                    {
                        DrawBorder(spriteBatch, cellRect, new Color(180, 180, 180, 200), 1);
                    }

                    if (UIItemRenderingHelper.TryCreateDisplayItem(itemId, out Item hoverItem))
                    {
                        Main.HoverItem = hoverItem.Clone();
                        Main.hoverItemName = hoverItem.Name;
                    }
                }
            }

            // Empty state
            if (_items.Count == 0)
            {
                Vector2 emptyStateSize = FontAssets.MouseText.Value.MeasureString(_emptyStateText);
                float emptyStateX = startX + (dims.Width - emptyStateSize.X) * 0.5f;
                Utils.DrawBorderString(spriteBatch, _emptyStateText,
                    new Vector2(emptyStateX, startY + 80f), Color.Gray);
            }
        }

        private bool TryGetItemIndexAtPosition(Vector2 mousePosition, out int index)
        {
            var dims = GetDimensions();
            GridLayout layout = GetLayout(dims);

            for (int i = 0; i < _items.Count && i < ItemsPerPage; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                Rectangle cellRect = GetCellRectangle(dims.X, dims.Y, layout, col, row);
                if (cellRect.Contains(mousePosition.ToPoint()))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private static GridLayout GetLayout(CalculatedStyle dims)
        {
            float cellWidth = GetCellWidth(dims.Width);
            return new GridLayout(cellWidth, cellWidth * CellAspectRatio);
        }

        private static float GetCellWidth(float availableWidth)
        {
            float clampedWidth = Math.Max(1f, availableWidth);
            float paddingWidth = (TargetColumns - 1) * CellPadding;
            return Math.Max(1f, (clampedWidth - paddingWidth) / TargetColumns);
        }

        private static Rectangle GetCellRectangle(float startX, float startY, GridLayout layout, int col, int row)
        {
            float x = startX + col * (layout.CellWidth + CellPadding);
            float y = startY + row * (layout.CellHeight + CellPadding);
            int left = (int)Math.Round(x);
            int top = (int)Math.Round(y);
            int right = (int)Math.Round(x + layout.CellWidth);
            int bottom = (int)Math.Round(y + layout.CellHeight);
            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private static void DrawItemName(SpriteBatch spriteBatch, int itemId, float x, float y, float maxWidth)
        {
            string name = UIItemRenderingHelper.GetDisplayNameOrFallback(itemId);

            float scale = 0.6f;

            // Truncate if name exceeds cell width
            Vector2 textSize = Utils.DrawBorderString(spriteBatch, name, Vector2.Zero, Color.Transparent, scale);
            if (textSize.X > maxWidth)
            {
                while (name.Length > 1)
                {
                    string candidate = name[..^1] + "..";
                    Vector2 candidateSize = Utils.DrawBorderString(spriteBatch, candidate, Vector2.Zero, Color.Transparent, scale);
                    if (candidateSize.X <= maxWidth)
                    {
                        name = candidate;
                        textSize = candidateSize;
                        break;
                    }
                    name = name[..^1];
                }
            }

            // Center horizontally within cell
            float textX = x + (maxWidth - textSize.X) / 2f;
            Utils.DrawBorderString(spriteBatch, name, new Vector2(textX, y), Color.White, scale);
        }

        private static void DrawItemIcon(SpriteBatch spriteBatch, int itemId, Vector2 center)
        {
            UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, itemId, center, 32f);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            var pixel = TextureAssets.MagicPixel.Value;
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
