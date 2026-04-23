using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Header bar above the recipe tree column. Displays a static "RECIPE TREE"
    /// label on the left, and optionally the selected item's display name.
    /// </summary>
    public class UIRecipeTreeHeader : UIElement
    {
        private const float TitleScale = 0.7f;
        private const float NameScale = 0.78f;
        private const float HorizontalPadding = 10f;
        private const float TitleNameGap = 10f;

        private readonly string _titleText;
        private string _selectedItemName = string.Empty;

        public UIRecipeTreeHeader(string titleText)
        {
            _titleText = titleText ?? string.Empty;
            IgnoresMouseInteraction = true;
        }

        public void SetSelectedItemName(string name)
        {
            _selectedItemName = name ?? string.Empty;
        }

        public void ClearSelectedItemName()
        {
            _selectedItemName = string.Empty;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle bounds = dimensions.ToRectangle();

            UIDrawHelper.DrawRect(spriteBatch, bounds, UIPalette.RecipeHeaderBg);
            UIDrawHelper.DrawRect(spriteBatch,
                new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1),
                UIPalette.RecipeHeaderBorder);

            float x = bounds.X + HorizontalPadding;
            float centerY = bounds.Y + bounds.Height * 0.5f;

            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(_titleText) * TitleScale;
            Vector2 titlePos = new(x, centerY - titleSize.Y * 0.5f);
            Utils.DrawBorderString(spriteBatch, _titleText, titlePos, UIPalette.RecipeHeaderTitle, TitleScale);

            if (!string.IsNullOrEmpty(_selectedItemName))
            {
                float nameX = x + titleSize.X + TitleNameGap;
                float maxNameWidth = bounds.Right - HorizontalPadding - nameX;
                string trimmed = TruncateToWidth(_selectedItemName, maxNameWidth, NameScale);
                Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(trimmed) * NameScale;
                Vector2 namePos = new(nameX, centerY - nameSize.Y * 0.5f);
                Utils.DrawBorderString(spriteBatch, trimmed, namePos, UIPalette.RecipeHeaderName, NameScale);
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
