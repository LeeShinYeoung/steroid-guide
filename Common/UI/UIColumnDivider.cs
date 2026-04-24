using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Thin 1px vertical divider used between columns in the Craftable panel.
    /// </summary>
    public class UIColumnDivider : UIElement
    {
        public UIColumnDivider()
        {
            IgnoresMouseInteraction = true;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            UIDrawHelper.DrawRect(spriteBatch, dimensions.ToRectangle(), UIPalette.Divider);
        }
    }
}
