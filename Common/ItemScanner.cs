using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common
{
    internal enum MessageType : byte
    {
        RequestChestContents,
        ChestContentsReady
    }

    public struct ScanResult
    {
        public Dictionary<int, int> Items;
        public int ChestCount;
    }

    public static class ItemScanner
    {
        private const float ScanRange = 60f * 16f;
        private const float ScanRangeSq = ScanRange * ScanRange;
        private const int MaxRequestsPerScan = 8;
        private const int ChestSyncTTLFrames = 3600; // 60s at 60fps

        private static readonly Dictionary<int, int> _syncedChestTimestamps = new();
        private static readonly HashSet<int> _requestedChests = new();
        private static int _frameCounter;

        public static void UpdateFrame()
        {
            _frameCounter++;
        }

        public static void MarkChestSynced(int chestIndex)
        {
            _syncedChestTimestamps[chestIndex] = _frameCounter;
            _requestedChests.Remove(chestIndex);
        }

        private static bool IsChestSynced(int chestIndex)
        {
            if (!_syncedChestTimestamps.TryGetValue(chestIndex, out int syncedAt))
                return false;
            if (_frameCounter - syncedAt > ChestSyncTTLFrames)
            {
                _syncedChestTimestamps.Remove(chestIndex);
                return false;
            }
            return true;
        }

        public static void ClearSyncState()
        {
            _syncedChestTimestamps.Clear();
            _requestedChests.Clear();
            _frameCounter = 0;
        }

        public static ScanResult ScanAvailableItems(Player player)
        {
            var items = new Dictionary<int, int>();
            int chestCount = 0;

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

            // Nearby chests within 60-tile radius of the player
            float playerX = player.Center.X;
            float playerY = player.Center.Y;
            bool isMultiplayer = Main.netMode == NetmodeID.MultiplayerClient;
            Mod mod = isMultiplayer ? ModContent.GetInstance<SteroidGuideMod>() : null;
            int requestsSent = 0;

            for (int i = 0; i < Main.maxChests; i++)
            {
                var chest = Main.chest[i];
                if (chest == null)
                    continue;

                float dx = chest.x * 16f + 16f - playerX;
                float dy = chest.y * 16f + 16f - playerY;
                if (dx * dx + dy * dy > ScanRangeSq)
                    continue;

                chestCount++;

                if (isMultiplayer && !IsChestSynced(i))
                {
                    if (mod != null && requestsSent < MaxRequestsPerScan && _requestedChests.Add(i))
                    {
                        var packet = mod.GetPacket();
                        packet.Write((byte)MessageType.RequestChestContents);
                        packet.Write(i);
                        packet.Send();
                        requestsSent++;
                    }
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

            return new ScanResult { Items = items, ChestCount = chestCount };
        }
    }
}
