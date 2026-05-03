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
            public readonly HashSet<int> VisibleIngredientIds;

            public CachedItemProps(Item item, HashSet<int> visibleIngredientIds)
            {
                NormalizedName = NormalizeSearchText(item.Name);
                Rare = item.rare;
                RarityScore = ComputeRarityScore(item.rare);
                Value = item.value;
                Category = ItemCategoryClassifier.Classify(item);
                VisibleIngredientIds = visibleIngredientIds;
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
                () => AnalyzeAndCollectVisible(graph, items, token),
                token);
        }

        // Walks the exact display tree each top-tier item would show on click, so search matches
        // the ingredients the user can actually see (owned intermediates cut the visible subtree).
        private static AnalysisResult AnalyzeAndCollectVisible(
            RecipeGraphData graph, Dictionary<int, int> available, CancellationToken ct)
        {
            var result = CraftableAnalyzer.Analyze(graph, available, ct);

            // Defensive: the main thread also reads `available` via OnItemSelected's BuildRecipeTree.
            // Display mode does not mutate the dict today, but a local copy decouples us from that assumption.
            var availableCopy = new Dictionary<int, int>(available);
            var visiting = new HashSet<int>();
            foreach (int topTierId in result.TopTierItems)
            {
                ct.ThrowIfCancellationRequested();

                visiting.Clear();
                var tree = CraftableAnalyzer.BuildRecipeTree(
                    topTierId, 1, graph, availableCopy, visiting,
                    ignoreOwnedForCurrentNode: true, ct);

                var ids = new HashSet<int>();
                CollectTreeItemIds(tree, ids, ct);
                result.VisibleIngredients[topTierId] = ids;
            }

            // Same display walk for partial top-tier items so search-by-ingredient and
            // _itemPropsCache cover Almost mode too. The display tree is built strict
            // (consumeAvailable=false, ignoreQuantity=false), exposing missing-quantity
            // ingredients as Missing nodes — exactly what we want indexed for search.
            foreach (int topTierId in result.PartialTopTierItems)
            {
                ct.ThrowIfCancellationRequested();

                visiting.Clear();
                var tree = CraftableAnalyzer.BuildRecipeTree(
                    topTierId, 1, graph, availableCopy, visiting,
                    ignoreOwnedForCurrentNode: true, ct);

                var ids = new HashSet<int>();
                CollectTreeItemIds(tree, ids, ct);
                result.VisibleIngredients[topTierId] = ids;
            }

            return result;
        }

        private static void CollectTreeItemIds(RecipeTreeNode node, HashSet<int> ids, CancellationToken ct)
        {
            if (node == null) return;
            ct.ThrowIfCancellationRequested();
            ids.Add(node.ItemId);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    CollectTreeItemIds(child, ids, ct);
            }
        }

        private void RebuildItemPropsCache()
        {
            _itemPropsCache.Clear();
            _ingredientNameCache.Clear();
            if (_analysisResult == null) return;

            CacheItemPropsFor(_analysisResult.TopTierItems);
            CacheItemPropsFor(_analysisResult.PartialTopTierItems);
        }

        private void CacheItemPropsFor(List<int> ids)
        {
            if (ids == null) return;

            foreach (int itemId in ids)
            {
                // Strict and partial top-tier sets are disjoint by construction, but guard
                // anyway so a future refactor doesn't double-cost SetDefaults.
                if (_itemPropsCache.ContainsKey(itemId))
                    continue;

                var item = new Item();
                item.SetDefaults(itemId);

                HashSet<int> visible = null;
                if (_analysisResult.VisibleIngredients != null)
                    _analysisResult.VisibleIngredients.TryGetValue(itemId, out visible);

                _itemPropsCache[itemId] = new CachedItemProps(item, visible);

                if (visible != null)
                {
                    foreach (int ingredientId in visible)
                    {
                        if (_ingredientNameCache.ContainsKey(ingredientId))
                            continue;
                        var ingredientItem = new Item();
                        ingredientItem.SetDefaults(ingredientId);
                        _ingredientNameCache[ingredientId] = NormalizeSearchText(ingredientItem.Name);
                    }
                }
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
