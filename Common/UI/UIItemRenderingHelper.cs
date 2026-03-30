using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common.UI
{
    internal static class UIItemRenderingHelper
    {
        public static bool TryCreateDisplayItem(int itemId, out Item item)
        {
            item = new Item();
            if (!IsSafeItemId(itemId))
                return false;

            try
            {
                item.SetDefaults(itemId);
                return item.type > ItemID.None;
            }
            catch
            {
                item = new Item();
                return false;
            }
        }

        public static string GetDisplayNameOrFallback(int itemId)
        {
            return TryCreateDisplayItem(itemId, out Item item)
                ? item.Name
                : $"Item #{itemId}";
        }

        public static bool TryDrawItemIcon(SpriteBatch spriteBatch, int itemId, Vector2 center, float maxDim)
        {
            if (!TryGetItemTexture(itemId, out Texture2D texture, out Rectangle frame))
                return false;

            float scale = 1f;
            if (frame.Width > maxDim || frame.Height > maxDim)
                scale = maxDim / Math.Max(frame.Width, frame.Height);

            spriteBatch.Draw(texture, center, frame, Color.White, 0f,
                frame.Size() / 2f, scale, SpriteEffects.None, 0f);
            return true;
        }

        private static bool TryGetItemTexture(int itemId, out Texture2D texture, out Rectangle frame)
        {
            texture = null;
            frame = Rectangle.Empty;

            if (!IsSafeItemId(itemId) || itemId >= TextureAssets.Item.Length)
                return false;

            Asset<Texture2D> asset = TextureAssets.Item[itemId];
            if (asset == null)
                return false;

            try
            {
                Main.instance.LoadItem(itemId);
                texture = asset.Value;
            }
            catch
            {
                texture = null;
                return false;
            }

            if (texture == null)
                return false;

            if (itemId < Main.itemAnimations.Length && Main.itemAnimations[itemId] != null)
            {
                frame = Main.itemAnimations[itemId].GetFrame(texture);
            }
            else
            {
                frame = texture.Frame();
            }

            return frame.Width > 0 && frame.Height > 0;
        }

        private static bool IsSafeItemId(int itemId)
        {
            return itemId > ItemID.None && itemId < ItemLoader.ItemCount;
        }
    }
}
