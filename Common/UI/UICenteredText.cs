using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UICenteredText : UIElement
    {
        private readonly float _scale;
        private string _text;

        public UICenteredText(string text, float scale)
        {
            _text = text ?? string.Empty;
            _scale = scale;
            Width.Set(0f, 1f);
            Height.Set(0f, 1f);
            IgnoresMouseInteraction = true;
        }

        public float MeasuredTextWidth => FontAssets.MouseText.Value.MeasureString(_text).X * _scale;

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (string.IsNullOrEmpty(_text))
                return;

            CalculatedStyle dimensions = GetDimensions();
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(_text) * _scale;
            Vector2 position = new(
                dimensions.X + (dimensions.Width - textSize.X) * 0.5f,
                dimensions.Y + (dimensions.Height - textSize.Y) * 0.5f);

            Utils.DrawBorderString(spriteBatch, _text, position, Color.White, _scale);
        }
    }
}
