using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public enum PaginationArrowDirection
    {
        Left,
        Right
    }

    public class UIPaginationArrowButton : UIElement
    {
        private static readonly int[] ArrowProfile = [1, 3, 5, 7, 9, 7, 5, 3, 1];
        private static readonly int ArrowWidth = GetArrowWidth();

        private readonly PaginationArrowDirection _direction;

        public bool IsEnabled { get; set; } = true;

        public UIPaginationArrowButton(PaginationArrowDirection direction)
        {
            _direction = direction;
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            var dimensions = GetDimensions();
            Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
            Color backgroundColor = IsEnabled
                ? (IsMouseHovering ? new Color(88, 109, 202) : new Color(63, 82, 151))
                : new Color(48, 48, 48);
            Color borderColor = IsEnabled
                ? (IsMouseHovering ? new Color(182, 194, 239) : new Color(121, 143, 214))
                : new Color(88, 88, 88);
            Color arrowColor = IsEnabled
                ? (IsMouseHovering ? Color.White : new Color(230, 230, 230))
                : new Color(140, 140, 140);

            UIDrawHelper.DrawRect(spriteBatch, bounds, backgroundColor);
            UIDrawHelper.DrawBorder(spriteBatch, bounds, borderColor, 1);
            DrawArrow(spriteBatch, bounds, arrowColor);
        }

        private void DrawArrow(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            Rectangle innerBounds = bounds;
            innerBounds.Inflate(-5, -4);
            int glyphHeight = ArrowProfile.Length;
            int glyphX = innerBounds.X + (innerBounds.Width - ArrowWidth) / 2;
            int glyphY = innerBounds.Y + (innerBounds.Height - glyphHeight) / 2;

            // A stepped, axis-aligned triangle stays readable under UI scaling and mirrors cleanly per direction.
            for (int row = 0; row < ArrowProfile.Length; row++)
            {
                int rowWidth = ArrowProfile[row];
                int rowX = _direction == PaginationArrowDirection.Left
                    ? glyphX + ArrowWidth - rowWidth
                    : glyphX;

                UIDrawHelper.DrawRect(spriteBatch, new Rectangle(rowX, glyphY + row, rowWidth, 1), color);
            }
        }

        private static int GetArrowWidth()
        {
            int width = 0;
            foreach (int rowWidth in ArrowProfile)
            {
                width = Math.Max(width, rowWidth);
            }

            return width;
        }


    }
}
