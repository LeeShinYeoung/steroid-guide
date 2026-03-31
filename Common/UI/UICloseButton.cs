using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UICloseButton : UIElement
    {
        private const int BorderThickness = 1;
        private const int InnerInset = 1;
        private const float IconThickness = 3f;
        private const float IconPadding = 9f;

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
            Vector2 center = PixelSnap(new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f));
            float halfSpan = MathF.Max(5f, (MathF.Min(bounds.Width, bounds.Height) - IconPadding * 2f) * 0.5f);

            Vector2 topLeft = PixelSnap(center + new Vector2(-halfSpan, -halfSpan));
            Vector2 bottomRight = PixelSnap(center + new Vector2(halfSpan, halfSpan));
            Vector2 bottomLeft = PixelSnap(center + new Vector2(-halfSpan, halfSpan));
            Vector2 topRight = PixelSnap(center + new Vector2(halfSpan, -halfSpan));

            DrawLine(spriteBatch, topLeft, bottomRight, color, IconThickness);
            DrawLine(spriteBatch, bottomLeft, topRight, color, IconThickness);
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

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            if (edge.LengthSquared() <= 0f)
            {
                return;
            }

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

        private static Vector2 PixelSnap(Vector2 point)
        {
            return new Vector2(MathF.Round(point.X), MathF.Round(point.Y));
        }
    }
}
