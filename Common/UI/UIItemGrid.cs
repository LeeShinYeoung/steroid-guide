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
            public GridLayout(float cellWidth, float cellHeight, int rowsPerPage)
            {
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                RowsPerPage = rowsPerPage;
            }

            public float CellWidth { get; }
            public float CellHeight { get; }
            public int RowsPerPage { get; }
        }

        private List<int> _items = new();
        private int _selectedItemId = -1;

        private const int TargetColumns = 5;
        private const int DefaultRowsPerPage = 6;
        private const float CellPadding = 2f;
        private const float IconMaxDim = 38f;
        private const float NameBottomPadding = 3f;
        private const float NameHorizontalPadding = 4f;
        private const float NameScale = 0.55f;
        private string _emptyStateText = "No craftable items found.";

        public event Action<int> OnItemSelected;
        public event Action<int> OnPageScrollRequested;

        public int Columns => TargetColumns;

        public int Rows
        {
            get
            {
                var dims = GetDimensions();
                float cellWidth = GetCellWidth(dims.Width);
                return ComputeRowsPerPage(dims.Height, cellWidth);
            }
        }

        public int ItemsPerPage => Columns * Rows;

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
            int itemsPerPage = Columns * layout.RowsPerPage;

            for (int i = 0; i < _items.Count && i < itemsPerPage; i++)
            {
                int row = i / Columns;
                int col = i % Columns;
                Rectangle cellRect = GetCellRectangle(startX, startY, layout, col, row);

                int itemId = _items[i];
                bool isSelected = itemId == _selectedItemId;
                bool isHovered = cellRect.Contains(Main.mouseX, Main.mouseY);

                // Cell background + border per state
                Color bgColor = isSelected
                    ? UIPalette.CellBgSelected
                    : (isHovered ? UIPalette.CellBgHover : UIPalette.CellBg);
                UIDrawHelper.DrawRect(spriteBatch, cellRect, bgColor);

                Color borderColor = isSelected
                    ? UIPalette.CellBorderSelected
                    : (isHovered ? UIPalette.CellBorderHover : UIPalette.CellBorder);
                int borderThickness = isSelected ? 2 : 1;
                UIDrawHelper.DrawBorder(spriteBatch, cellRect, borderColor, borderThickness);

                // Icon: centered above the name label
                float iconAreaHeight = cellRect.Height - 12f; // leave ~12px for name label
                float iconCenterY = cellRect.Y + iconAreaHeight * 0.5f;
                DrawItemIcon(spriteBatch, itemId, new Vector2(cellRect.X + cellRect.Width * 0.5f, iconCenterY));

                // Name label along the bottom edge
                float nameY = cellRect.Bottom - NameBottomPadding -
                              FontAssets.MouseText.Value.MeasureString("X").Y * NameScale;
                DrawItemName(spriteBatch, itemId,
                    cellRect.X + NameHorizontalPadding, nameY,
                    cellRect.Width - NameHorizontalPadding * 2f, isSelected);

                if (isHovered && UIItemRenderingHelper.TryCreateDisplayItem(itemId, out Item hoverItem))
                {
                    Main.HoverItem = hoverItem.Clone();
                    Main.hoverItemName = hoverItem.Name;
                }
            }

            // Empty state
            if (_items.Count == 0)
            {
                Vector2 emptyStateSize = FontAssets.MouseText.Value.MeasureString(_emptyStateText);
                float emptyStateX = startX + (dims.Width - emptyStateSize.X) * 0.5f;
                float emptyStateY = startY + (dims.Height - emptyStateSize.Y) * 0.5f;
                Utils.DrawBorderString(spriteBatch, _emptyStateText,
                    new Vector2(emptyStateX, emptyStateY), Color.Gray);
            }
        }

        private bool TryGetItemIndexAtPosition(Vector2 mousePosition, out int index)
        {
            var dims = GetDimensions();
            GridLayout layout = GetLayout(dims);
            int itemsPerPage = Columns * layout.RowsPerPage;

            for (int i = 0; i < _items.Count && i < itemsPerPage; i++)
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
            float cellHeight = cellWidth; // square cells per mock
            int rowsPerPage = ComputeRowsPerPage(dims.Height, cellHeight);
            return new GridLayout(cellWidth, cellHeight, rowsPerPage);
        }

        private static int ComputeRowsPerPage(float availableHeight, float cellHeight)
        {
            if (availableHeight <= 0f || cellHeight <= 0f)
                return DefaultRowsPerPage;
            return Math.Max(1, (int)((availableHeight + CellPadding) / (cellHeight + CellPadding)));
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

        private static void DrawItemName(SpriteBatch spriteBatch, int itemId, float x, float y, float maxWidth, bool isSelected)
        {
            string name = UIItemRenderingHelper.GetDisplayNameOrFallback(itemId);
            float scale = NameScale;

            // Truncate if name exceeds cell width
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(name) * scale;
            if (textSize.X > maxWidth)
            {
                while (name.Length > 1)
                {
                    string candidate = name[..^1] + "..";
                    Vector2 candidateSize = FontAssets.MouseText.Value.MeasureString(candidate) * scale;
                    if (candidateSize.X <= maxWidth)
                    {
                        name = candidate;
                        textSize = candidateSize;
                        break;
                    }
                    name = name[..^1];
                    textSize = FontAssets.MouseText.Value.MeasureString(name) * scale;
                }
            }

            float textX = x + (maxWidth - textSize.X) / 2f;
            Color color = isSelected ? UIPalette.CellNameTextSelected : UIPalette.CellNameText;
            Utils.DrawBorderString(spriteBatch, name, new Vector2(textX, y), color, scale);
        }

        private static void DrawItemIcon(SpriteBatch spriteBatch, int itemId, Vector2 center)
        {
            UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, itemId, center, IconMaxDim);
        }
    }
}
