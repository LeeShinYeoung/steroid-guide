using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    /// <summary>
    /// Drop-in replacement for <see cref="UIPanel"/> that bypasses the vanilla
    /// 9-slice panel texture (which has slightly rounded corners) and instead
    /// renders a flat filled rectangle with a 1px border. Used for the main
    /// window so the outer chrome matches the sharp-cornered inner column
    /// backdrops drawn via <see cref="UIDrawHelper.DrawRect"/>.
    /// </summary>
    public class UIRectPanel : UIPanel
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();
            var rect = dimensions.ToRectangle();
            UIDrawHelper.DrawRect(spriteBatch, rect, BackgroundColor);
            UIDrawHelper.DrawBorder(spriteBatch, rect, BorderColor, 1);
        }
    }
}
