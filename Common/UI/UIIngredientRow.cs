using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// A flat-row ingredient display rendered under an expanded recipe tree node.
    /// Shows the ingredient's icon, name, and `have/need` with color coding driven
    /// by the live scan snapshot (lookup is called each frame; no rebuild on scan change).
    /// </summary>
    public class UIIngredientRow : UIElement
    {
        private const float IconSize = 20f;
        private const float NameScale = 0.7f;
        private const float HaveScale = 0.75f;
        private const float NeedScale = 0.65f;
        private const float LeftPadding = 8f;
        private const float RightPadding = 10f;
        private const float IconBorderPadding = 2f;
        private const float IconNameGap = 8f;
        private const float StockGap = 3f;

        private readonly int _ingredientId;
        private readonly int _needed;
        private readonly Func<int, int> _getHaveCount;
        private readonly float _leftIndent;

        public UIIngredientRow(int ingredientId, int needed, Func<int, int> getHaveCount, float leftIndent = 0f)
        {
            _ingredientId = ingredientId;
            _needed = Math.Max(0, needed);
            _getHaveCount = getHaveCount;
            _leftIndent = Math.Max(0f, leftIndent);

            Width.Set(0f, 1f);
            Height.Set(24f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle fullBounds = dimensions.ToRectangle();
            Rectangle bounds = new(
                fullBounds.X + (int)_leftIndent,
                fullBounds.Y,
                Math.Max(1, fullBounds.Width - (int)_leftIndent),
                fullBounds.Height);

            // Subtle row background + thin bottom separator to group ingredient stacks.
            UIDrawHelper.DrawRect(spriteBatch, bounds, UIPalette.IngRowBg);
            UIDrawHelper.DrawRect(spriteBatch,
                new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1),
                UIPalette.IngRowSeparator);

            // Icon box
            float iconLeft = bounds.X + LeftPadding;
            float iconTop = bounds.Y + (bounds.Height - IconSize) * 0.5f;
            var iconRect = new Rectangle(
                (int)iconLeft,
                (int)iconTop,
                (int)IconSize,
                (int)IconSize);
            UIDrawHelper.DrawRect(spriteBatch, iconRect, UIPalette.IngIconBg);
            UIDrawHelper.DrawBorder(spriteBatch, iconRect, UIPalette.IngIconBorder, 1);

            Vector2 iconCenter = new(iconRect.X + iconRect.Width * 0.5f, iconRect.Y + iconRect.Height * 0.5f);
            UIItemRenderingHelper.TryDrawItemIcon(spriteBatch, _ingredientId, iconCenter, IconSize - IconBorderPadding * 2f);

            // Right-aligned have/need
            int have = _getHaveCount != null ? Math.Max(0, _getHaveCount(_ingredientId)) : 0;
            Color haveColor = _needed <= 0
                ? UIPalette.StockOk
                : (have >= _needed
                    ? UIPalette.StockOk
                    : (have > 0 ? UIPalette.StockWarn : UIPalette.StockBad));

            string haveStr = have.ToString(CultureInfo.InvariantCulture);
            string sepStr = "/";
            string needStr = _needed.ToString(CultureInfo.InvariantCulture);

            Vector2 needSize = FontAssets.MouseText.Value.MeasureString(needStr) * NeedScale;
            Vector2 sepSize = FontAssets.MouseText.Value.MeasureString(sepStr) * NeedScale;
            Vector2 haveSize = FontAssets.MouseText.Value.MeasureString(haveStr) * HaveScale;

            float rowCenterY = bounds.Y + bounds.Height * 0.5f;
            float stockRight = bounds.Right - RightPadding;
            float needX = stockRight - needSize.X;
            float sepX = needX - StockGap - sepSize.X;
            float haveX = sepX - StockGap - haveSize.X;

            Utils.DrawBorderString(spriteBatch, needStr,
                new Vector2(needX, rowCenterY - needSize.Y * 0.5f),
                UIPalette.IngNeed, NeedScale);
            Utils.DrawBorderString(spriteBatch, sepStr,
                new Vector2(sepX, rowCenterY - sepSize.Y * 0.5f),
                UIPalette.IngSeparator, NeedScale);
            Utils.DrawBorderString(spriteBatch, haveStr,
                new Vector2(haveX, rowCenterY - haveSize.Y * 0.5f),
                haveColor, HaveScale);

            // Name fills between icon and stock indicator
            float nameLeft = iconRect.Right + IconNameGap;
            float nameMaxRight = haveX - 6f;
            float nameMaxWidth = Math.Max(0f, nameMaxRight - nameLeft);

            string name = UIItemRenderingHelper.GetDisplayNameOrFallback(_ingredientId);
            string trimmedName = TruncateToWidth(name, nameMaxWidth, NameScale);
            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(trimmedName) * NameScale;
            Vector2 namePos = new(nameLeft, rowCenterY - nameSize.Y * 0.5f);
            Utils.DrawBorderString(spriteBatch, trimmedName, namePos, UIPalette.IngName, NameScale);

            // Hover: show tooltip for the ingredient item so players can identify modded items.
            if (bounds.Contains(Main.mouseX, Main.mouseY) &&
                UIItemRenderingHelper.TryCreateDisplayItem(_ingredientId, out Item hoverItem))
            {
                Main.HoverItem = hoverItem.Clone();
                Main.hoverItemName = hoverItem.Name;
            }
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
