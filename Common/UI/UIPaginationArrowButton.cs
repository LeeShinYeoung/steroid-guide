using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public enum PaginationArrowDirection
    {
        Left,
        Right
    }

    public class UIPaginationArrowButton : UIElement
    {
        private readonly PaginationArrowDirection _direction;

        public bool IsEnabled { get; set; } = true;

        public UIPaginationArrowButton(PaginationArrowDirection direction)
        {
            _direction = direction;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            var dimensions = GetDimensions();
            Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
            Color backgroundColor = IsEnabled
                ? (IsMouseHovering ? new Color(88, 109, 202) : new Color(63, 82, 151))
                : new Color(48, 48, 48);
            Color borderColor = IsEnabled
                ? (IsMouseHovering ? new Color(182, 194, 239) : new Color(121, 143, 214))
                : new Color(88, 88, 88);
            Color arrowColor = IsEnabled
                ? (IsMouseHovering ? Color.White : new Color(230, 230, 230))
                : new Color(140, 140, 140);

            DrawRect(spriteBatch, bounds, backgroundColor);
            DrawBorder(spriteBatch, bounds, borderColor, 1);
            DrawArrow(spriteBatch, bounds, arrowColor);
        }

        private void DrawArrow(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            Vector2 center = new(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f);
            float arrowHalfWidth = MathF.Max(5f, bounds.Width * 0.18f);
            float arrowHalfHeight = MathF.Max(5f, bounds.Height * 0.24f);
            float thickness = 3f;
            float directionSign = _direction == PaginationArrowDirection.Left ? -1f : 1f;

            Vector2[] chevronPoints =
            [
                new(-arrowHalfWidth, -arrowHalfHeight),
                new(arrowHalfWidth, 0f),
                new(-arrowHalfWidth, arrowHalfHeight)
            ];

            for (int i = 0; i < chevronPoints.Length; i++)
            {
                Vector2 point = chevronPoints[i];
                point.X *= directionSign;
                chevronPoints[i] = PixelSnap(center + point);
            }

            DrawLine(spriteBatch, chevronPoints[0], chevronPoints[1], color, thickness);
            DrawLine(spriteBatch, chevronPoints[1], chevronPoints[2], color, thickness);
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
