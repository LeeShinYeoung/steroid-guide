using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Sidebar category row rendered per the redesign mock: checkbox on the left,
    /// label in the middle, optional count badge on the right. Active rows get a
    /// highlighted background and an inset 3px left accent bar.
    /// </summary>
    public class UICategoryRow : UIElement
    {
        private const float TextScale = 0.75f;
        private const float BadgeTextScale = 0.7f;
        private const int CheckSize = 12;
        private const int CheckInnerInset = 3;
        private const int AccentWidth = 3;
        private const float CheckLeftPadding = 10f;
        private const float LabelLeftPadding = 30f;
        private const float BadgeRightPadding = 10f;

        private readonly string _label;
        private string _badgeText = string.Empty;
        private bool _hasBadge;
        private bool _selected;

        public UICategoryRow(string label)
        {
            _label = label ?? string.Empty;
            Width.Set(0f, 1f);
            Height.Set(26f, 0f);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
        }

        public void SetBadgeCount(int count)
        {
            _hasBadge = true;
            _badgeText = count.ToString(CultureInfo.InvariantCulture);
        }

        public void ClearBadge()
        {
            _hasBadge = false;
            _badgeText = string.Empty;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle bounds = dimensions.ToRectangle();

            // Row background
            if (_selected)
            {
                UIDrawHelper.DrawRect(spriteBatch, bounds, UIPalette.CatRowActiveBg);
                UIDrawHelper.DrawBorder(spriteBatch, bounds, UIPalette.CatRowBorderActive, 1);
                UIDrawHelper.DrawRect(spriteBatch,
                    new Rectangle(bounds.X, bounds.Y, AccentWidth, bounds.Height),
                    UIPalette.CatRowAccent);
            }
            else if (IsMouseHovering)
            {
                UIDrawHelper.DrawRect(spriteBatch, bounds, UIPalette.CatRowHoverBg);
                UIDrawHelper.DrawBorder(spriteBatch, bounds, UIPalette.CatRowBorder, 1);
            }

            // Checkbox
            var checkRect = new Rectangle(
                (int)(bounds.X + CheckLeftPadding),
                bounds.Y + (bounds.Height - CheckSize) / 2,
                CheckSize,
                CheckSize);

            Color checkBg = _selected ? UIPalette.CatCheckActiveBg : UIPalette.CatCheckBg;
            Color checkBorder = _selected ? UIPalette.CatCheckActiveBorder : UIPalette.CatCheckBorder;
            UIDrawHelper.DrawRect(spriteBatch, checkRect, checkBg);
            UIDrawHelper.DrawBorder(spriteBatch, checkRect, checkBorder, 1);

            if (_selected)
            {
                Rectangle innerCheck = checkRect;
                innerCheck.Inflate(-CheckInnerInset, -CheckInnerInset);
                UIDrawHelper.DrawRect(spriteBatch, innerCheck, UIPalette.CatCheckInner);
            }

            // Badge (right-aligned)
            float badgeRight = bounds.Right - BadgeRightPadding;
            if (_hasBadge && !string.IsNullOrEmpty(_badgeText))
            {
                Color badgeColor = _selected ? UIPalette.CatRowBadgeActive : UIPalette.CatRowBadge;
                Vector2 badgeSize = FontAssets.MouseText.Value.MeasureString(_badgeText) * BadgeTextScale;
                Vector2 badgePos = new(
                    badgeRight - badgeSize.X,
                    bounds.Y + (bounds.Height - badgeSize.Y) * 0.5f);
                Utils.DrawBorderString(spriteBatch, _badgeText, badgePos, badgeColor, BadgeTextScale);
                badgeRight -= badgeSize.X + 6f;
            }

            // Label (between checkbox and badge)
            Color labelColor = _selected ? UIPalette.CatRowLabelActive : UIPalette.CatRowLabel;
            float labelX = bounds.X + LabelLeftPadding;
            float labelMaxWidth = badgeRight - labelX;
            string labelText = TruncateToWidth(_label, labelMaxWidth, TextScale);
            Vector2 labelSize = FontAssets.MouseText.Value.MeasureString(labelText) * TextScale;
            Vector2 labelPos = new(
                labelX,
                bounds.Y + (bounds.Height - labelSize.Y) * 0.5f);
            Utils.DrawBorderString(spriteBatch, labelText, labelPos, labelColor, TextScale);
        }

        private static string TruncateToWidth(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
                return string.Empty;

            float width = FontAssets.MouseText.Value.MeasureString(text).X * scale;
            if (width <= maxWidth)
                return text;

            string truncated = text;
            while (truncated.Length > 1)
            {
                truncated = truncated[..^1];
                string candidate = truncated + "..";
                if (FontAssets.MouseText.Value.MeasureString(candidate).X * scale <= maxWidth)
                    return candidate;
            }
            return truncated;
        }
    }
}
