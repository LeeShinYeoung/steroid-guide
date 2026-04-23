using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Translucent flat-color background element. Used to tint column areas
    /// without composing full UIPanel chrome. Ignores mouse input so it doesn't
    /// steal clicks from siblings.
    /// </summary>
    public class UIColorBackdrop : UIElement
    {
        private readonly Color _color;

        public UIColorBackdrop(Color color)
        {
            _color = color;
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            UIDrawHelper.DrawRect(spriteBatch, dimensions.ToRectangle(), _color);
        }
    }
}
