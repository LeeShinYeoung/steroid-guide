using System.Collections.Generic;
using SteroidGuide.Common;
using Terraria;

namespace SteroidGuide.Common.UI
{
    public partial class CraftableUIState
    {
        private readonly struct CachedItemProps
        {
            public readonly string NormalizedName;
            public readonly int Rare;
            public readonly int Value;
            public readonly FilterCategory Category;

            public CachedItemProps(Item item)
            {
                NormalizedName = NormalizeSearchText(item.Name);
                Rare = item.rare;
                Value = item.value;
                Category = ItemCategoryClassifier.Classify(item);
            }
        }

        private void RunAnalysis()
        {
            if (Main.LocalPlayer == null) return;
            ApplyScanResult(ItemScanner.ScanAvailableItems(Main.LocalPlayer), forceAnalysis: true);
        }

        private void ApplyScanResult(ScanResult scanResult, bool forceAnalysis = false)
        {
            bool itemsChanged = forceAnalysis ||
                _analysisResult == null ||
                !_latestScanResult.HasValue ||
                !DictEquals(_latestScanResult.Value.Items, scanResult.Items);

            _latestScanResult = scanResult;
            UpdateNearbyChestStatusText(scanResult.ChestCount);

            if (!itemsChanged)
            {
                return;
            }

            var graph = RecipeGraphSystem.Graph;
            if (graph == null || scanResult.Items == null) return;

            _analysisResult = CraftableAnalyzer.Analyze(graph, scanResult.Items);
            RebuildItemPropsCache();
            ApplyFilter();
        }

        private void RebuildItemPropsCache()
        {
            _itemPropsCache.Clear();
            _recipeDepthCache.Clear();
            if (_analysisResult == null) return;

            var graph = RecipeGraphSystem.Graph;
            foreach (int itemId in _analysisResult.TopTierItems)
            {
                var item = new Item();
                item.SetDefaults(itemId);
                _itemPropsCache[itemId] = new CachedItemProps(item);

                if (graph != null)
                    _recipeDepthCache[itemId] = CraftableAnalyzer.GetRecipeDepth(itemId, graph);
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
