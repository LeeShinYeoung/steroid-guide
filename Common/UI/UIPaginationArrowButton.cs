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
                ? (IsMouseHovering ? UIPalette.PageBtnBgHover : UIPalette.PageBtnBg)
                : new Color(22, 24, 46, 180);
            Color borderColor = IsEnabled
                ? (IsMouseHovering ? UIPalette.PageBtnBorderHover : UIPalette.PageBtnBorder)
                : new Color(42, 46, 76);
            Color arrowColor = IsEnabled
                ? (IsMouseHovering ? UIPalette.PageBtnArrowHover : UIPalette.PageBtnArrow)
                : new Color(64, 72, 112);

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
