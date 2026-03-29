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
            Vector2 center = new(bounds.Center.X, bounds.Center.Y);
            float arrowHalfWidth = MathF.Max(5f, bounds.Width * 0.18f);
            float arrowHalfHeight = MathF.Max(5f, bounds.Height * 0.24f);
            float thickness = 3f;

            if (_direction == PaginationArrowDirection.Left)
            {
                Vector2 tip = new(center.X - arrowHalfWidth, center.Y);
                Vector2 top = new(center.X + arrowHalfWidth, center.Y - arrowHalfHeight);
                Vector2 bottom = new(center.X + arrowHalfWidth, center.Y + arrowHalfHeight);
                DrawLine(spriteBatch, top, tip, color, thickness);
                DrawLine(spriteBatch, tip, bottom, color, thickness);
            }
            else
            {
                Vector2 tip = new(center.X + arrowHalfWidth, center.Y);
                Vector2 top = new(center.X - arrowHalfWidth, center.Y - arrowHalfHeight);
                Vector2 bottom = new(center.X - arrowHalfWidth, center.Y + arrowHalfHeight);
                DrawLine(spriteBatch, top, tip, color, thickness);
                DrawLine(spriteBatch, tip, bottom, color, thickness);
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

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            float angle = MathF.Atan2(edge.Y, edge.X);
            var destination = new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, destination, null, color, angle, Vector2.Zero, SpriteEffects.None, 0f);
        }
    }
}
