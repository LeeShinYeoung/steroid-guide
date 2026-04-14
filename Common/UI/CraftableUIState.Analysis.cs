using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SteroidGuide.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common.UI
{
    public partial class CraftableUIState
    {
        private static readonly Dictionary<string, int> ModRarityTierMap = new()
        {
            ["CalamityMod/Turquoise"] = 12,
            ["CalamityMod/PureGreen"] = 13,
            ["CalamityMod/CosmicPurple"] = 14,
            ["CalamityMod/DarkOrange"] = 15,
            ["CalamityMod/BurnishedAuric"] = 15,
            ["CalamityMod/HotPink"] = 16,
            ["CalamityMod/CalamityRed"] = 17,
        };

        private readonly struct CachedItemProps
        {
            public readonly string NormalizedName;
            public readonly int Rare;
            public readonly int RarityScore;
            public readonly int Value;
            public readonly FilterCategory Category;

            public CachedItemProps(Item item)
            {
                NormalizedName = NormalizeSearchText(item.Name);
                Rare = item.rare;
                RarityScore = ComputeRarityScore(item.rare);
                Value = item.value;
                Category = ItemCategoryClassifier.Classify(item);
            }
        }

        private static int ComputeRarityScore(int rare)
        {
            // Modded rarity: registration order is unreliable, use explicit mapping
            if (rare >= ItemRarityID.Count)
            {
                var modRarity = RarityLoader.GetRarity(rare);
                if (modRarity != null && ModRarityTierMap.TryGetValue(modRarity.FullName, out int tier))
                    return tier;

                // Unknown mod rarity: place just above vanilla max
                return ItemRarityID.Count;
            }

            // Special vanilla rarities
            return rare switch
            {
                -13 => 11, // Master → endgame tier
                -12 => 11, // Expert → endgame tier
                -11 => 0,  // Quest → white tier
                _ => rare,
            };
        }

        private void RunAnalysis()
        {
            if (Main.LocalPlayer == null) return;
            DispatchAnalysis(ItemScanner.ScanAvailableItems(Main.LocalPlayer));
        }

        private void RunAnalysisFromLatestScan()
        {
            if (!_latestScanResult.HasValue) return;
            DispatchAnalysis(_latestScanResult.Value);
        }

        private void DispatchAnalysis(ScanResult scanResult)
        {
            var graph = RecipeGraphSystem.Graph;
            if (graph == null || scanResult.Items == null) return;

            var oldCts = _analysisCts;
            _analysisCts = new CancellationTokenSource();
            oldCts?.Cancel();
            oldCts?.Dispose();
            var token = _analysisCts.Token;

            // ItemScanner.ScanAvailableItems returns a fresh dict per call. CraftableAnalyzer.Analyze
            // snapshots `available` internally and mutates only its own working copy — the input dict
            // is left untouched, so main-thread reads of _latestScanResult.Value.Items (e.g. HasScanChanged
            // and BuildRecipeTree for recipe tree display) are safe while the task runs.
            var items = scanResult.Items;

            _pendingAnalysisTask = Task.Run(
                () => CraftableAnalyzer.Analyze(graph, items, token),
                token);
        }

        private void RebuildItemPropsCache()
        {
            _itemPropsCache.Clear();
            if (_analysisResult == null) return;

            foreach (int itemId in _analysisResult.TopTierItems)
            {
                var item = new Item();
                item.SetDefaults(itemId);
                _itemPropsCache[itemId] = new CachedItemProps(item);
            }
        }

        private static string NormalizeSearchText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToUpperInvariant();
        }

        private bool HasScanChanged(ScanResult scanResult)
        {
            return _analysisResult == null ||
                !_latestScanResult.HasValue ||
                _latestScanResult.Value.ChestCount != scanResult.ChestCount ||
                !DictEquals(_latestScanResult.Value.Items, scanResult.Items);
        }

        private static bool DictEquals(Dictionary<int, int> a, Dictionary<int, int> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            foreach (var (key, val) in a)
            {
                if (!b.TryGetValue(key, out int bv) || val != bv)
                    return false;
            }
            return true;
        }
    }
}
