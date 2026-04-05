using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UISortButton : UIElement
    {
        private const float TextScale = 0.72f;
        private const int AccentWidth = 4;
        private const int IconLineHeight = 2;
        private const int IconDotSize = 3;
        private const int IconGap = 4;
        private const float IconLeftPadding = 12f;
        private const float LabelLeftPadding = 36f;

        private string _label = string.Empty;
        private bool _open;

        public UISortButton()
        {
            Width.Set(0f, 1f);
            Height.Set(28f, 0f);
        }

        public void SetState(string label, bool open)
        {
            _label = label ?? string.Empty;
            _open = open;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle bounds = dimensions.ToRectangle();

            Color backgroundColor = _open
                ? new Color(68, 88, 150, 224)
                : IsMouseHovering
                    ? new Color(48, 60, 102, 222)
                    : new Color(33, 42, 73, 215);
            Color borderColor = _open
                ? new Color(198, 210, 250, 228)
                : IsMouseHovering
                    ? new Color(150, 167, 218, 210)
                    : new Color(118, 136, 195, 185);
            Color accentColor = _open
                ? new Color(255, 220, 120)
                : IsMouseHovering
                    ? new Color(160, 178, 236)
                    : new Color(94, 108, 154, 170);
            Color iconColor = _open
                ? new Color(255, 242, 182)
                : IsMouseHovering
                    ? new Color(236, 241, 255)
                    : new Color(212, 220, 248);
            Color textColor = _open
                ? Color.White
                : IsMouseHovering
                    ? new Color(238, 242, 255)
                    : new Color(220, 225, 245);

            UIDrawHelper.DrawRect(spriteBatch, bounds, backgroundColor);
            UIDrawHelper.DrawRect(spriteBatch, new Rectangle(bounds.X, bounds.Y, AccentWidth, bounds.Height), accentColor);
            UIDrawHelper.DrawBorder(spriteBatch, bounds, borderColor, 1);

            DrawSortIcon(spriteBatch, bounds, iconColor);

            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(_label) * TextScale;
            Vector2 labelPosition = new(
                bounds.X + LabelLeftPadding,
                bounds.Y + (bounds.Height - labelSize.Y) * 0.5f);
            Utils.DrawBorderString(spriteBatch, _label, labelPosition, textColor, TextScale);
        }

        private static void DrawSortIcon(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            int centerY = bounds.Center.Y;
            int x = (int)(bounds.X + IconLeftPadding);
            int[] widths = [12, 9, 6];

            for (int i = 0; i < widths.Length; i++)
            {
                int y = centerY - IconGap + i * IconGap;
                UIDrawHelper.DrawRect(spriteBatch, new Rectangle(x, y, IconDotSize, IconDotSize), color);
                UIDrawHelper.DrawRect(spriteBatch, new Rectangle(x + IconDotSize + 2, y, widths[i], IconLineHeight), color);
            }
        }
    }
}
