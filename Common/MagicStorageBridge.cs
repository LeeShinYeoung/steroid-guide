using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common
{
    // Single point of contact with the Magic Storage assembly.
    //
    // All direct uses of MagicStorage.* types live inside MergeNearbyHeartItems_Impl,
    // which is marked NoInlining so the JIT only resolves those types when the impl
    // method actually executes. Combined with the ModLoader.HasMod gate in IsAvailable,
    // a missing or incompatible Magic Storage install never breaks our load.
    //
    // The impl method's body is wrapped in a single try/catch (TypeLoadException,
    // MissingMethodException, etc.) that logs once and returns 0, so a future
    // MS API rename degrades us to chests-only rather than crashing the scan.
    internal static class MagicStorageBridge
    {
        private static bool? _isAvailable;
        private static bool _loggedFailure;

        public static bool IsAvailable
        {
            get
            {
                if (_isAvailable.HasValue) return _isAvailable.Value;
                _isAvailable = ModLoader.HasMod("MagicStorage");
                return _isAvailable.Value;
            }
        }

        // Called from CraftableUISystem.OnWorldUnload so we re-probe per session
        // (covers the user disabling/enabling MS between worlds without quitting).
        public static void ResetGate()
        {
            _isAvailable = null;
            _loggedFailure = false;
        }

        // Returns the number of Hearts found in range. Their stored items are merged
        // into target. Short-circuits to 0 if Magic Storage is not loaded or if the
        // bridge call throws.
        //
        // playerX / playerY are pixel-space (matches the chest scan distance check).
        // scanRangeSq is the same value ItemScanner uses for chests (60 tiles squared).
        public static int MergeNearbyHeartItems(float playerX, float playerY,
            float scanRangeSq, Dictionary<int, int> target)
        {
            if (!IsAvailable) return 0;
            return MergeNearbyHeartItems_Impl(playerX, playerY, scanRangeSq, target);
        }

        // CRITICAL: this is the ONLY method that touches MagicStorage.* types.
        // NoInlining keeps those type references out of MergeNearbyHeartItems' frame,
        // so the JIT does not try to resolve them when MS is missing.
        [JITWhenModsEnabled("MagicStorage")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int MergeNearbyHeartItems_Impl(float playerX, float playerY,
            float scanRangeSq, Dictionary<int, int> target)
        {
            try
            {
                // 1) collect every live Heart in range
                List<MagicStorage.Components.TEStorageHeart> hearts = null;
                foreach (var kv in TileEntity.ByPosition)
                {
                    if (kv.Value is not MagicStorage.Components.TEStorageHeart heart)
                        continue;
                    if (!heart.IsAlive)
                        continue;

                    float hx = heart.Position.X * 16f + 16f;
                    float hy = heart.Position.Y * 16f + 16f;
                    float dx = hx - playerX;
                    float dy = hy - playerY;
                    if (dx * dx + dy * dy > scanRangeSq)
                        continue;

                    hearts ??= new List<MagicStorage.Components.TEStorageHeart>();
                    hearts.Add(heart);
                }

                if (hearts == null)
                    return 0;

                // 2) dedup units across all reachable Hearts (GetStorageUnits includes
                //    RemoteAccess targets, so two Hearts pointing at the same warehouse
                //    must not double-count).
                var seenUnits = new HashSet<Point16>();
                foreach (var heart in hearts)
                {
                    foreach (var unit in heart.GetStorageUnits())
                    {
                        if (unit == null) continue;
                        if (!seenUnits.Add(unit.Position)) continue;

                        foreach (var item in unit.GetItems())
                        {
                            if (item == null || item.IsAir) continue;
                            if (item.type <= ItemID.None || item.stack <= 0) continue;

                            target.TryGetValue(item.type, out int count);
                            target[item.type] = count + item.stack;
                        }
                    }
                }

                return hearts.Count;
            }
            catch (Exception ex) when (
                ex is TypeLoadException ||
                ex is MissingMethodException ||
                ex is MissingMemberException ||
                ex is MemberAccessException ||
                ex is FileNotFoundException ||
                ex is BadImageFormatException ||
                ex is InvalidCastException)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    var mod = ModLoader.TryGetMod("SteroidGuide", out var sg) ? sg : null;
                    mod?.Logger?.Warn(
                        "MagicStorageBridge degraded: " + ex.GetType().Name + ": " + ex.Message);
                }
                return 0;
            }
        }
    }
}
