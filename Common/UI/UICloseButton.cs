using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UICloseButton : UIElement
    {
        private static readonly int[] IconDiagonalOffsets = [0, 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1, 0];
        private const int BorderThickness = 1;
        private const int InnerInset = 1;
        private const int IconStrokeWidth = 2;

        public UICloseButton()
        {
            Width.Set(30f, 0f);
            Height.Set(30f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            Rectangle bounds = GetDimensions().ToRectangle();
            Rectangle innerBounds = bounds;
            innerBounds.Inflate(-InnerInset, -InnerInset);

            Color backgroundColor = IsMouseHovering
                ? new Color(76, 96, 170, 228)
                : new Color(33, 42, 73, 215);
            Color innerFillColor = IsMouseHovering
                ? new Color(93, 117, 202, 212)
                : new Color(45, 57, 98, 225);
            Color borderColor = IsMouseHovering
                ? new Color(198, 210, 250, 228)
                : new Color(118, 136, 195, 185);
            Color iconColor = IsMouseHovering
                ? Color.White
                : new Color(228, 234, 252);

            DrawRect(spriteBatch, bounds, backgroundColor);
            DrawRect(spriteBatch, innerBounds, innerFillColor);
            DrawBorder(spriteBatch, bounds, borderColor, BorderThickness);
            DrawCloseIcon(spriteBatch, bounds, iconColor);
        }

        private static void DrawCloseIcon(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            int glyphHeight = IconDiagonalOffsets.Length;
            int maxOffset = IconDiagonalOffsets[(glyphHeight - 1) / 2];
            int glyphWidth = maxOffset * 2 + IconStrokeWidth;
            int glyphX = bounds.X + (bounds.Width - glyphWidth) / 2;
            int glyphY = bounds.Y + (bounds.Height - glyphHeight) / 2;

            for (int row = 0; row < glyphHeight; row++)
            {
                int offset = IconDiagonalOffsets[row];
                int rowY = glyphY + row;
                int leftX = glyphX + offset;
                int rightX = glyphX + glyphWidth - offset - IconStrokeWidth;

                DrawRect(spriteBatch, new Rectangle(leftX, rowY, IconStrokeWidth, 1), color);
                if (rightX > leftX)
                {
                    DrawRect(spriteBatch, new Rectangle(rightX, rowY, IconStrokeWidth, 1), color);
                }
            }
        }

        private static void DrawRect(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, rectangle, color);
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
        {
            DrawRect(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
            DrawRect(spriteBatch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
            DrawRect(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
            DrawRect(spriteBatch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
        }

    }
}
