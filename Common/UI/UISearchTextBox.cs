using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public class UISearchTextBox : UIElement
    {
        private const float HorizontalPadding = 10f;
        private const float VerticalPadding = 8f;
        private const float TextScale = 0.8f;

        private readonly string _placeholderText;
        private readonly int _maxLength;
        private string _text = string.Empty;

        public event Action<string> OnTextChanged;

        public bool IsFocused { get; private set; }
        public string Text => _text;

        public UISearchTextBox(string placeholderText, int maxLength = 64)
        {
            _placeholderText = placeholderText ?? string.Empty;
            _maxLength = Math.Max(1, maxLength);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            Focus();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            Rectangle bounds = dims.ToRectangle();

            Color backgroundColor = IsFocused
                ? new Color(48, 52, 86, 230)
                : new Color(30, 34, 56, 215);
            Color borderColor = IsFocused
                ? new Color(180, 205, 255)
                : new Color(90, 110, 150);

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, backgroundColor);
            DrawBorder(spriteBatch, bounds, borderColor, 2);

            bool drawPlaceholder = string.IsNullOrEmpty(_text);
            float maxTextWidth = Math.Max(0f, dims.Width - HorizontalPadding * 2f);
            Vector2 textPosition = new(dims.X + HorizontalPadding, dims.Y + VerticalPadding);

            if (drawPlaceholder)
            {
                string placeholderText = TrimTextToFit(_placeholderText, maxTextWidth);
                Utils.DrawBorderString(spriteBatch, placeholderText, textPosition, new Color(150, 150, 150), TextScale);
            }
            else
            {
                string typedText = ShouldDrawCaret()
                    ? $"{_text}|"
                    : _text;
                string trimmedText = TrimTextToFit(typedText, maxTextWidth);
                Utils.DrawBorderString(spriteBatch, trimmedText, textPosition, Color.White, TextScale);
            }
        }

        public void Reset()
        {
            Unfocus();
            SetText(string.Empty);
        }

        public void Focus()
        {
            if (IsFocused)
            {
                return;
            }

            IsFocused = true;
            PlayerInput.WritingText = true;

            if (Main.LocalPlayer != null)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            Main.clrInput();
        }

        public void Unfocus()
        {
            IsFocused = false;
        }

        public bool HandleEscape()
        {
            if (!IsFocused)
            {
                return false;
            }

            Unfocus();
            return true;
        }

        public void UpdateFocusedTextInput()
        {
            if (!IsFocused)
            {
                return;
            }

            if (Main.mouseLeft && Main.mouseLeftRelease && !ContainsPoint(Main.MouseScreen))
            {
                Unfocus();
                return;
            }

            if (Main.LocalPlayer != null)
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            PlayerInput.WritingText = true;

            string updatedText = Main.GetInputText(_text);
            SetText(updatedText);
        }

        private void SetText(string newText)
        {
            newText ??= string.Empty;
            if (newText.Length > _maxLength)
            {
                newText = newText[.._maxLength];
            }

            if (string.Equals(_text, newText, StringComparison.Ordinal))
            {
                return;
            }

            _text = newText;
            OnTextChanged?.Invoke(_text);
        }

        private bool ShouldDrawCaret()
        {
            return IsFocused && (int)(Main.GlobalTimeWrappedHourly * 2f) % 2 == 0;
        }

        private static string TrimTextToFit(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            {
                return string.Empty;
            }

            string candidate = text;
            while (candidate.Length > 1)
            {
                Vector2 size = FontAssets.MouseText.Value.MeasureString(candidate) * TextScale;
                if (size.X <= maxWidth)
                {
                    return candidate;
                }

                candidate = candidate[..^1];
            }

            return candidate;
        }

        private static void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
