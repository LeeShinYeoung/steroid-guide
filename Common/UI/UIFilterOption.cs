using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UIFilterOption : UIElement
    {
        private readonly string _label;
        private bool _selected;

        private const float TextScale = 0.75f;
        private const int IndicatorSize = 12;
        private const int IndicatorBorderThickness = 2;
        private const float IndicatorLeftPadding = 8f;
        private const float LabelLeftPadding = 28f;

        public UIFilterOption(string label)
        {
            _label = label;
            Width.Set(0f, 1f);
            Height.Set(24f, 0f);
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

            Color backgroundColor = _selected
                ? new Color(74, 93, 162, 210)
                : new Color(36, 41, 68, IsMouseHovering ? 190 : 150);
            Color borderColor = _selected
                ? new Color(170, 188, 242)
                : new Color(98, 110, 164, IsMouseHovering ? 210 : 160);
            Color textColor = _selected ? Color.White : new Color(220, 225, 245);
            Color indicatorBorderColor = _selected
                ? new Color(230, 236, 255)
                : new Color(155, 168, 214);

            DrawRect(spriteBatch, bounds, backgroundColor);
            DrawBorder(spriteBatch, bounds, borderColor, 1);

            var indicatorRect = new Rectangle(
                (int)(bounds.X + IndicatorLeftPadding),
                bounds.Center.Y - IndicatorSize / 2,
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

            Vector2 labelPosition = new(bounds.X + LabelLeftPadding, bounds.Y + 4f);
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
