using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace SteroidGuide.Common
{
    public static class ItemScanner
    {
        public static Dictionary<int, int> ScanAvailableItems(Player player)
        {
            var items = new Dictionary<int, int>();

            // Player inventory slots 0-57 (hotbar + inventory + coins + ammo)
            for (int i = 0; i < 58; i++)
            {
                var item = player.inventory[i];
                if (item != null && item.type > ItemID.None && item.stack > 0)
                {
                    items.TryGetValue(item.type, out int count);
                    items[item.type] = count + item.stack;
                }
            }

            // On-screen chests — account for game zoom to get the actual visible world area
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            float screenLeft = Main.screenPosition.X;
            float screenRight = screenLeft + Main.screenWidth / zoom.X;
            float screenTop = Main.screenPosition.Y;
            float screenBottom = screenTop + Main.screenHeight / zoom.Y;

            for (int i = 0; i < Main.maxChests; i++)
            {
                var chest = Main.chest[i];
                if (chest == null)
                    continue;

                float chestX = chest.x * 16f;
                float chestY = chest.y * 16f;

                if (chestX < screenLeft || chestX > screenRight ||
                    chestY < screenTop || chestY > screenBottom)
                    continue;

                // Multiplayer: chest metadata (position) is synced, but item contents
                // may not be until the client has opened the chest. Detect unsynced
                // chests (all item slots empty) and request contents from the server.
                if (Main.netMode == NetmodeID.MultiplayerClient && IsChestUnsynced(chest))
                {
                    NetMessage.SendData(MessageID.RequestChestOpen, -1, -1, null, i);
                    continue;
                }

                foreach (var item in chest.item)
                {
                    if (item != null && item.type > ItemID.None && item.stack > 0)
                    {
                        items.TryGetValue(item.type, out int count);
                        items[item.type] = count + item.stack;
                    }
                }
            }

            return items;
        }

        private static bool IsChestUnsynced(Chest chest)
        {
            for (int i = 0; i < chest.item.Length; i++)
            {
                if (chest.item[i] != null && chest.item[i].type > ItemID.None)
                    return false;
            }
            return true;
        }
    }
}
