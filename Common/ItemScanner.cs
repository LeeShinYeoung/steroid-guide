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
        public int SyncedChestCount;
    }

    public static class ItemScanner
    {
        private const float ScanRange = 60f * 16f;
        private const float ScanRangeSq = ScanRange * ScanRange;
        private const int MaxRequestsPerScan = 32;
        private const int ChestSyncTTLFrames = 3600; // 60s at 60fps

        // Guards _syncedChestTimestamps, _requestedChests, and _chestContents.
        // Writers can run on the network thread (MarkChestSynced, UpdateChestContentsFromMainChest);
        // readers and most mutators run on the main thread (ScanAvailableItems, ClearSyncState, UpdateFrame).
        private static readonly object _syncLock = new();

        private static readonly Dictionary<int, int> _syncedChestTimestamps = new();
        private static readonly HashSet<int> _requestedChests = new();

        // Per-chest item snapshot cache keyed by chest index.
        // Value is an aggregated type -> total stack map, which is what ScanAvailableItems needs.
        // Survives TTL expiry so the scan keeps producing results while a refresh is in flight.
        private static readonly Dictionary<int, Dictionary<int, int>> _chestContents = new();

        private static int _frameCounter;

        public static void UpdateFrame()
        {
            _frameCounter++;
        }

        public static void MarkChestSynced(int chestIndex)
        {
            lock (_syncLock)
            {
                _syncedChestTimestamps[chestIndex] = _frameCounter;
                _requestedChests.Remove(chestIndex);
            }
        }

        // Snapshots Main.chest[chestIndex].item into the cache.
        // Called from the network thread (packet handler) after vanilla per-slot sync has populated
        // Main.chest[chestIndex].item, and BEFORE MarkChestSynced so readers see the new contents
        // together with the new timestamp.
        public static void UpdateChestContentsFromMainChest(int chestIndex)
        {
            if (chestIndex < 0 || chestIndex >= Main.maxChests)
                return;

            var chest = Main.chest[chestIndex];
            if (chest == null || chest.item == null)
                return;

            // Build the snapshot OUTSIDE the lock; only swap references under the lock.
            var snapshot = new Dictionary<int, int>();
            foreach (var item in chest.item)
            {
                if (item != null && item.type > ItemID.None && item.stack > 0)
                {
                    snapshot.TryGetValue(item.type, out int count);
                    snapshot[item.type] = count + item.stack;
                }
            }

            lock (_syncLock)
            {
                _chestContents[chestIndex] = snapshot;
            }
        }

        public static void ClearSyncState()
        {
            lock (_syncLock)
            {
                _syncedChestTimestamps.Clear();
                _requestedChests.Clear();
                _chestContents.Clear();
                _frameCounter = 0;
            }
        }

        // Must be called with _syncLock held.
        private static bool IsChestCacheFresh_NoLock(int chestIndex)
        {
            if (!_syncedChestTimestamps.TryGetValue(chestIndex, out int syncedAt))
                return false;
            return _frameCounter - syncedAt <= ChestSyncTTLFrames;
        }

        public static ScanResult ScanAvailableItems(Player player)
        {
            var items = new Dictionary<int, int>();
            int chestCount = 0;
            int syncedChestCount = 0;

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

                if (isMultiplayer)
                {
                    Dictionary<int, int> cachedSnapshot;
                    bool hasCache;
                    bool fresh;
                    lock (_syncLock)
                    {
                        hasCache = _chestContents.TryGetValue(i, out cachedSnapshot);
                        fresh = hasCache && IsChestCacheFresh_NoLock(i);
                    }

                    if (!hasCache)
                    {
                        // First-time encounter: request and skip. No data to contribute yet.
                        if (mod != null && requestsSent < MaxRequestsPerScan)
                        {
                            bool added;
                            lock (_syncLock)
                            {
                                added = _requestedChests.Add(i);
                            }
                            if (added)
                            {
                                var packet = mod.GetPacket();
                                packet.Write((byte)MessageType.RequestChestContents);
                                packet.Write(i);
                                packet.Send();
                                requestsSent++;
                            }
                        }
                        continue;
                    }

                    if (!fresh)
                    {
                        // Stale: fire a refresh in the background but keep using the cached snapshot.
                        if (mod != null && requestsSent < MaxRequestsPerScan)
                        {
                            bool added;
                            lock (_syncLock)
                            {
                                added = _requestedChests.Add(i);
                            }
                            if (added)
                            {
                                var packet = mod.GetPacket();
                                packet.Write((byte)MessageType.RequestChestContents);
                                packet.Write(i);
                                packet.Send();
                                requestsSent++;
                            }
                        }
                    }

                    syncedChestCount++;

                    // Dictionary is immutable once installed (writers replace the reference under the lock),
                    // so iterating outside the lock is safe.
                    foreach (var kv in cachedSnapshot)
                    {
                        items.TryGetValue(kv.Key, out int count);
                        items[kv.Key] = count + kv.Value;
                    }
                    continue;
                }

                // Singleplayer path: read Main.chest[i].item live. Cache is untouched.
                syncedChestCount++;

                foreach (var item in chest.item)
                {
                    if (item != null && item.type > ItemID.None && item.stack > 0)
                    {
                        items.TryGetValue(item.type, out int count);
                        items[item.type] = count + item.stack;
                    }
                }
            }

            // --- Magic Storage merge (weak reference; no-op when MS is not loaded) ---
            // Hearts within ScanRangeSq contribute their flattened unit contents.
            // Each Heart counts as one entry in both ChestCount and SyncedChestCount —
            // MS handles its own MP sync, so any Heart we can read is "synced" from our side.
            int heartCount = MagicStorageBridge.MergeNearbyHeartItems(
                playerX, playerY, ScanRangeSq, items);
            chestCount += heartCount;
            syncedChestCount += heartCount;

            // --- Personal banks (vanilla, always-on; no chest counter bump) ---
            // Piggy Bank, Safe, Defender's Forge, Void Vault. Items persist in these
            // arrays even when the corresponding tile/bag is not currently in use.
            MergePersonalBank(player.bank?.item, items);
            MergePersonalBank(player.bank2?.item, items);
            MergePersonalBank(player.bank3?.item, items);
            MergePersonalBank(player.bank4?.item, items);

            return new ScanResult
            {
                Items = items,
                ChestCount = chestCount,
                SyncedChestCount = syncedChestCount
            };
        }

        private static void MergePersonalBank(Item[] slots, Dictionary<int, int> target)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var item = slots[i];
                if (item == null || item.type <= ItemID.None || item.stack <= 0) continue;
                target.TryGetValue(item.type, out int count);
                target[item.type] = count + item.stack;
            }
        }
    }
}
