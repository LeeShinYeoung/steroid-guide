using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common.UI
{
    internal static class UIItemRenderingHelper
    {
        // itemId → probe Item cache. Avoids repeated Item.SetDefaults (expensive) per frame
        // for callers that read RarityColor on every Draw. Null entries are sentinels for
        // ids that failed SetDefaults (modded items that throw, etc.) so retries are cheap.
        // Cleared on Unload via ClearCaches() so stale references after mod reload are dropped.
        private static readonly Dictionary<int, Item> ProbeCache = new();

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

        /// <summary>
        /// Returns the rarity color for the given item id. Modded rarities are resolved via
        /// <see cref="RarityLoader.GetRarity(int)"/> + <see cref="ModRarity.RarityColor"/>;
        /// vanilla rare values (incl. Master/Expert/Quest dynamic colors via DiscoColor) fall
        /// through to <see cref="ItemRarity.GetColor(int)"/>. Animated overlays driven via
        /// <c>ModifyTooltips</c> overrides (e.g. Calamity Auric / Cosmilite shifting) are NOT
        /// reflected here — only static rarity color matching. Returns <paramref name="fallback"/>
        /// when the id is invalid, the probe failed, or the modded RarityColor getter throws.
        /// </summary>
        public static Color GetItemNameColor(int itemId, Color fallback)
        {
            Item probe = GetProbe(itemId);
            if (probe == null || probe.type <= ItemID.None)
                return fallback;

            try
            {
                ModRarity modRarity = RarityLoader.GetRarity(probe.rare);
                if (modRarity != null)
                    return modRarity.RarityColor;

                return ItemRarity.GetColor(probe.rare);
            }
            catch
            {
                return fallback;
            }
        }

        public static void ClearCaches()
        {
            ProbeCache.Clear();
        }

        private static Item GetProbe(int itemId)
        {
            if (ProbeCache.TryGetValue(itemId, out Item cached))
                return cached;

            if (!IsSafeItemId(itemId))
            {
                ProbeCache[itemId] = null;
                return null;
            }

            try
            {
                var item = new Item();
                item.SetDefaults(itemId);
                if (item.type <= ItemID.None)
                {
                    ProbeCache[itemId] = null;
                    return null;
                }
                ProbeCache[itemId] = item;
                return item;
            }
            catch
            {
                ProbeCache[itemId] = null;
                return null;
            }
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
