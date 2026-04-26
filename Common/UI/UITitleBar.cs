using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Titlebar container for the Craftable panel. Renders the navy gradient-style
    /// background and the 1px bottom accent. Children (status text, close button)
    /// are appended by the caller.
    /// </summary>
    public class UITitleBar : UIElement
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dimensions = GetDimensions();
            Rectangle bounds = dimensions.ToRectangle();

            UIDrawHelper.DrawRect(spriteBatch, bounds, UIPalette.TitleBarBg);
            UIDrawHelper.DrawRect(spriteBatch,
                new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1),
                UIPalette.TitleBarAccent);
        }
    }

    /// <summary>
    /// Transparent slot for the nearby-chest status text in the titlebar.
    /// Sized to its parent (1.0 width/height) — defines the text bounds without any background.
    /// </summary>
    public class UITitleBarStatusPill : UIElement
    {
    }
}
