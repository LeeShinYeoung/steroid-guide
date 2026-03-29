using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UIItemGrid : UIElement
    {
        private List<int> _items = new();
        private int _selectedItemId = -1;
        private const int Rows = 4;
        private const float CellWidth = 48f;
        private const float CellHeight = 60f;
        private const float CellPadding = 6f;

        public event Action<int> OnItemSelected;

        /// <summary>
        /// Dynamically computed column count based on the element's actual width.
        /// </summary>
        public int Columns
        {
            get
            {
                float availableWidth = GetDimensions().Width;
                if (availableWidth <= 0f)
                    return 1;
                // First cell needs CellWidth, each additional cell needs CellWidth + CellPadding
                int cols = Math.Max(1, (int)((availableWidth + CellPadding) / (CellWidth + CellPadding)));
                return cols;
            }
        }

        public int ItemsPerPage => Columns * Rows;

        public void SetItems(List<int> items, int selectedId)
        {
            _items = items ?? new List<int>();
            _selectedItemId = selectedId;
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);

            var dims = GetDimensions();
            float relX = evt.MousePosition.X - dims.X;
            float relY = evt.MousePosition.Y - dims.Y;

            int col = (int)(relX / (CellWidth + CellPadding));
            int row = (int)(relY / (CellHeight + CellPadding));

            int columns = Columns;
            if (col >= 0 && col < columns && row >= 0 && row < Rows)
            {
                int index = row * columns + col;
                if (index < _items.Count)
                {
                    OnItemSelected?.Invoke(_items[index]);
                }
            }
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var dims = GetDimensions();
            float startX = dims.X;
            float startY = dims.Y;

            int columns = Columns;
            for (int i = 0; i < _items.Count && i < columns * Rows; i++)
            {
                int row = i / columns;
                int col = i % columns;

                float x = startX + col * (CellWidth + CellPadding);
                float y = startY + row * (CellHeight + CellPadding);
                var cellRect = new Rectangle((int)x, (int)y, (int)CellWidth, (int)CellHeight);

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
                float iconCenterY = y + 20f;
                DrawItemIcon(spriteBatch, itemId, new Vector2(x + CellWidth / 2f, iconCenterY));

                // Draw item name below icon
                DrawItemName(spriteBatch, itemId, x, y + 38f, CellWidth);

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

                    var hoverItem = new Item();
                    hoverItem.SetDefaults(itemId);
                    Main.HoverItem = hoverItem.Clone();
                    Main.hoverItemName = hoverItem.Name;
                }
            }

            // Empty state
            if (_items.Count == 0)
            {
                Utils.DrawBorderString(spriteBatch, "No craftable items found.",
                    new Vector2(startX + 20f, startY + 80f), Color.Gray);
            }
        }

        private static void DrawItemName(SpriteBatch spriteBatch, int itemId, float x, float y, float maxWidth)
        {
            var item = new Item();
            item.SetDefaults(itemId);
            string name = item.Name;

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
            Main.instance.LoadItem(itemId);
            var texture = TextureAssets.Item[itemId].Value;

            Rectangle frame;
            if (Main.itemAnimations[itemId] != null)
                frame = Main.itemAnimations[itemId].GetFrame(texture);
            else
                frame = texture.Frame();

            float maxDim = 32f;
            float scale = 1f;
            if (frame.Width > maxDim || frame.Height > maxDim)
                scale = maxDim / Math.Max(frame.Width, frame.Height);

            spriteBatch.Draw(texture, center, frame, Color.White, 0f,
                frame.Size() / 2f, scale, SpriteEffects.None, 0f);
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
