using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UISortOption : UIElement
    {
        private const float TextScale = 0.75f;
        private const int IndicatorSize = 12;
        private const int IndicatorBorderThickness = 2;
        private const int RowAccentWidth = 4;
        private const float IndicatorLeftPadding = 10f;
        private const float LabelLeftPadding = 32f;

        private readonly string _label;
        private bool _selected;

        public UISortOption(string label)
        {
            _label = label;
            Width.Set(0f, 1f);
            Height.Set(28f, 0f);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle bounds = dimensions.ToRectangle();
            Rectangle rowBounds = new(bounds.X, bounds.Y, bounds.Width, Math.Max(0, bounds.Height - 1));

            Color backgroundColor = _selected
                ? new Color(78, 98, 172, 222)
                : IsMouseHovering
                    ? new Color(46, 56, 90, 214)
                    : new Color(29, 36, 60, 165);
            Color borderColor = _selected
                ? new Color(182, 198, 244, 220)
                : new Color(118, 132, 188, 155);
            Color separatorColor = _selected
                ? new Color(180, 194, 238, 210)
                : IsMouseHovering
                    ? new Color(110, 124, 176, 170)
                    : new Color(84, 96, 144, 110);
            Color accentColor = _selected
                ? new Color(255, 220, 120)
                : IsMouseHovering
                    ? new Color(160, 178, 236)
                    : new Color(92, 104, 156, 90);
            Color textColor = _selected
                ? Color.White
                : IsMouseHovering
                    ? new Color(236, 240, 252)
                    : new Color(220, 225, 245);
            Color indicatorBorderColor = _selected
                ? new Color(230, 236, 255)
                : IsMouseHovering
                    ? new Color(182, 194, 236)
                    : new Color(148, 160, 206);

            DrawRect(spriteBatch, rowBounds, backgroundColor);
            DrawRect(spriteBatch, new Rectangle(rowBounds.X, rowBounds.Y, RowAccentWidth, rowBounds.Height), accentColor);
            DrawRect(spriteBatch, new Rectangle(rowBounds.X, rowBounds.Bottom - 1, rowBounds.Width, 1), separatorColor);

            if (_selected || IsMouseHovering)
            {
                DrawBorder(spriteBatch, rowBounds, borderColor, 1);
            }

            var indicatorRect = new Rectangle(
                (int)(rowBounds.X + IndicatorLeftPadding),
                rowBounds.Y + (rowBounds.Height - IndicatorSize) / 2,
                IndicatorSize,
                IndicatorSize);

            DrawRect(spriteBatch, indicatorRect, new Color(24, 29, 48, 230));
            DrawBorder(spriteBatch, indicatorRect, indicatorBorderColor, IndicatorBorderThickness);

            if (_selected)
            {
                Rectangle fillRect = indicatorRect;
                fillRect.Inflate(-4, -4);
                DrawRect(spriteBatch, fillRect, new Color(255, 220, 120));
            }

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(_label) * TextScale;
            Vector2 labelPosition = new(
                rowBounds.X + LabelLeftPadding,
                rowBounds.Y + (rowBounds.Height - labelSize.Y) * 0.5f);
            Utils.DrawBorderString(spriteBatch, _label, labelPosition, textColor, TextScale);
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
