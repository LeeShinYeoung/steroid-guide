using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace SteroidGuide.Common
{
    // Reads the items stored in nearby Magic Storage networks.
    //
    // SteroidGuide.dll must not contain any compile-time reference to Magic Storage. The runtime
    // resolves type and assembly references lazily (JIT, GC, AssemblyLoadContext unload), and when
    // Magic Storage is not loaded that resolution kills the game on mod reload; no attribute or
    // try/catch can intercept it. So the project does not reference Magic Storage at all: the
    // members below are looked up by name once all mods are loaded and dropped again on unload.
    //
    // Magic Storage offers no Mod.Call for reading stored items, so these are its internal members.
    // If any of them is missing or throws, the integration turns itself off until the next reload
    // (logged once) and the scan simply covers chests only.
    public sealed class MagicStorageBridge : ModSystem
    {
        private const string HeartTypeName = "MagicStorage.Components.TEStorageHeart";
        private const string UnitTypeName = "MagicStorage.Components.TEAbstractStorageUnit";

        private sealed class StorageApi
        {
            public Type HeartType;
            public PropertyInfo IsAlive;       // bool TEStorageHeart.IsAlive
            public MethodInfo GetStorageUnits; // IEnumerable<TEAbstractStorageUnit> TEStorageHeart.GetStorageUnits()
            public MethodInfo GetItems;        // IEnumerable<Item> TEAbstractStorageUnit.GetItems(), virtual dispatch via Invoke
        }

        private static StorageApi _api;

        public override void PostSetupContent() => _api = Resolve();

        public override void Unload() => _api = null;

        private StorageApi Resolve()
        {
            if (!ModLoader.TryGetMod("MagicStorage", out Mod magicStorage))
                return null;

            const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;
            Type heart = magicStorage.Code?.GetType(HeartTypeName, throwOnError: false);
            Type unit = magicStorage.Code?.GetType(UnitTypeName, throwOnError: false);
            var api = new StorageApi
            {
                HeartType = heart,
                IsAlive = heart?.GetProperty("IsAlive", Public, null, typeof(bool), Type.EmptyTypes, null),
                GetStorageUnits = heart?.GetMethod("GetStorageUnits", Public, Type.EmptyTypes),
                GetItems = unit?.GetMethod("GetItems", Public, Type.EmptyTypes),
            };

            if (api.IsAlive == null || api.GetStorageUnits == null || api.GetItems == null
                || !typeof(IEnumerable).IsAssignableFrom(api.GetStorageUnits.ReturnType)
                || !typeof(IEnumerable).IsAssignableFrom(api.GetItems.ReturnType))
            {
                Mod.Logger.Warn($"Magic Storage {magicStorage.Version} is loaded but its storage members have changed; " +
                    "nearby Magic Storage will not be scanned.");
                return null;
            }

            Mod.Logger.Info($"Magic Storage {magicStorage.Version} found; nearby storage networks will be scanned.");
            return api;
        }

        // Adds the items of every live Storage Heart within range, including the units it reaches
        // remotely, to `target` and returns how many Hearts were found. Nothing is added if the
        // scan fails part-way. Coordinates are in pixels; rangeSq matches the chest scan radius.
        public static int MergeNearbyHeartItems(float playerX, float playerY, float rangeSq, Dictionary<int, int> target)
        {
            var api = _api;
            if (api == null)
                return 0;

            try
            {
                int hearts = 0;
                Dictionary<int, int> found = null;
                // Two Hearts can reach the same units, so each unit is counted once.
                HashSet<Point16> seenUnits = null;

                foreach (var entity in TileEntity.ByPosition.Values)
                {
                    if (!api.HeartType.IsInstanceOfType(entity) || !(bool)api.IsAlive.GetValue(entity))
                        continue;

                    // Hearts are 2x2 tiles: measure from their centre.
                    float dx = entity.Position.X * 16f + 16f - playerX;
                    float dy = entity.Position.Y * 16f + 16f - playerY;
                    if (dx * dx + dy * dy > rangeSq)
                        continue;

                    hearts++;
                    found ??= new Dictionary<int, int>();
                    seenUnits ??= new HashSet<Point16>();

                    foreach (object unit in (IEnumerable)api.GetStorageUnits.Invoke(entity, null))
                    {
                        if (unit is not TileEntity unitEntity || !seenUnits.Add(unitEntity.Position))
                            continue;

                        foreach (object stored in (IEnumerable)api.GetItems.Invoke(unit, null))
                        {
                            if (stored is Item { IsAir: false } item)
                            {
                                found.TryGetValue(item.type, out int count);
                                found[item.type] = count + item.stack;
                            }
                        }
                    }
                }

                if (found != null)
                {
                    foreach (var (type, stack) in found)
                    {
                        target.TryGetValue(type, out int count);
                        target[type] = count + stack;
                    }
                }
                return hearts;
            }
            catch (Exception e)
            {
                // Best effort only: Magic Storage must never take the scan, or the game, down with it.
                _api = null;
                ModContent.GetInstance<SteroidGuideMod>()?.Logger.Warn(
                    "Reading Magic Storage failed; nearby storage will not be scanned until mods are reloaded.", e);
                return 0;
            }
        }
    }
}
