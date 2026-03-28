using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public enum FilterCategory
    {
        All,
        Weapons,
        Armor,
        Accessories,
        Potions,
        Tools,
        Misc
    }

    public class RecipeAnalyzerUIState : UIState
    {
        private UIPanel _mainPanel;

        // Filter
        private FilterCategory _currentFilter = FilterCategory.All;
        private readonly Dictionary<FilterCategory, UIText> _filterButtons = new();

        // Item grid
        private UIItemGrid _itemGrid;
        private UIText _pageText;

        // Recipe tree
        private UIRecipeTree _recipeTree;

        // State
        private AnalysisResult _analysisResult;
        private Dictionary<int, int> _lastScannedItems;
        private List<int> _filteredItems = new();
        private int _currentPage;
        private int _totalPages = 1;
        private int _selectedItemId = -1;
        private int _updateCounter;
        private const int ItemsPerPage = 20;

        public override void OnInitialize()
        {
            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(820f, 0f);
            _mainPanel.Height.Set(600f, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.SetPadding(12f);

            // Title
            var titleText = new UIText("Steroid Guide - Recipe Analyzer", 0.8f, true);
            titleText.Top.Set(5f, 0f);
            titleText.HAlign = 0.5f;
            _mainPanel.Append(titleText);

            // Close button [X]
            var closeButton = new UITextPanel<string>("X", 0.8f, false);
            closeButton.Width.Set(30f, 0f);
            closeButton.Height.Set(30f, 0f);
            closeButton.Top.Set(2f, 0f);
            closeButton.Left.Set(-30f, 1f);
            closeButton.SetPadding(4f);
            closeButton.OnLeftClick += (evt, el) =>
            {
                ModContent.GetInstance<RecipeAnalyzerUISystem>()?.HideUI();
            };
            _mainPanel.Append(closeButton);

            // ── Filter sidebar ──
            var filterPanel = new UIPanel();
            filterPanel.Top.Set(42f, 0f);
            filterPanel.Left.Set(0f, 0f);
            filterPanel.Width.Set(120f, 0f);
            filterPanel.Height.Set(220f, 0f);
            filterPanel.SetPadding(6f);
            _mainPanel.Append(filterPanel);

            float filterY = 0f;
            foreach (FilterCategory cat in Enum.GetValues(typeof(FilterCategory)))
            {
                string prefix = cat == _currentFilter ? "[*]" : "[ ]";
                var btn = new UIText($"{prefix} {cat}", 0.75f);
                btn.Top.Set(filterY, 0f);
                var captured = cat;
                btn.OnLeftClick += (evt, el) => SetFilter(captured);
                filterPanel.Append(btn);
                _filterButtons[cat] = btn;
                filterY += 28f;
            }

            // ── Item grid ──
            _itemGrid = new UIItemGrid();
            _itemGrid.Top.Set(42f, 0f);
            _itemGrid.Left.Set(132f, 0f);
            _itemGrid.Width.Set(650f, 0f);
            _itemGrid.Height.Set(264f, 0f);
            _itemGrid.OnItemSelected += OnItemSelected;
            _mainPanel.Append(_itemGrid);

            // ── Pagination ──
            var prevBtn = new UITextPanel<string>("<", 0.7f, false);
            prevBtn.Width.Set(30f, 0f);
            prevBtn.Height.Set(24f, 0f);
            prevBtn.Top.Set(312f, 0f);
            prevBtn.Left.Set(340f, 0f);
            prevBtn.SetPadding(4f);
            prevBtn.OnLeftClick += (evt, el) => ChangePage(-1);
            _mainPanel.Append(prevBtn);

            _pageText = new UIText("Page 1/1", 0.75f);
            _pageText.Top.Set(315f, 0f);
            _pageText.Left.Set(380f, 0f);
            _mainPanel.Append(_pageText);

            var nextBtn = new UITextPanel<string>(">", 0.7f, false);
            nextBtn.Width.Set(30f, 0f);
            nextBtn.Height.Set(24f, 0f);
            nextBtn.Top.Set(312f, 0f);
            nextBtn.Left.Set(470f, 0f);
            nextBtn.SetPadding(4f);
            nextBtn.OnLeftClick += (evt, el) => ChangePage(1);
            _mainPanel.Append(nextBtn);

            // ── Recipe tree (bottom half) ──
            _recipeTree = new UIRecipeTree();
            _recipeTree.Top.Set(344f, 0f);
            _recipeTree.Left.Set(0f, 0f);
            _recipeTree.Width.Set(0f, 1f);
            _recipeTree.Height.Set(230f, 0f);
            _mainPanel.Append(_recipeTree);

            Append(_mainPanel);
        }

        public void OnShow()
        {
            _lastScannedItems = null;
            _selectedItemId = -1;
            _currentPage = 0;
            _recipeTree?.ClearTree();
            RunAnalysis();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Throttle inventory change detection to every ~30 frames
            _updateCounter++;
            if (_updateCounter % 30 == 0 && Main.LocalPlayer != null)
            {
                var currentItems = ItemScanner.ScanAvailableItems(Main.LocalPlayer);
                if (!DictEquals(_lastScannedItems, currentItems))
                {
                    _lastScannedItems = currentItems;
                    RunAnalysisFromScan();
                }
            }

            // Block world interaction when hovering our panel
            if (_mainPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private void RunAnalysis()
        {
            if (Main.LocalPlayer == null) return;
            _lastScannedItems = ItemScanner.ScanAvailableItems(Main.LocalPlayer);
            RunAnalysisFromScan();
        }

        private void RunAnalysisFromScan()
        {
            var graph = RecipeGraphSystem.Graph;
            if (graph == null || _lastScannedItems == null) return;

            _analysisResult = RecipeAnalyzer.Analyze(graph, _lastScannedItems);
            ApplyFilter();
        }

        private void SetFilter(FilterCategory category)
        {
            _currentFilter = category;
            foreach (var (cat, btn) in _filterButtons)
            {
                string prefix = cat == _currentFilter ? "[*]" : "[ ]";
                btn.SetText($"{prefix} {cat}");
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_analysisResult == null) return;

            _filteredItems.Clear();
            foreach (int itemId in _analysisResult.TopTierItems)
            {
                if (_currentFilter == FilterCategory.All || GetItemCategory(itemId) == _currentFilter)
                    _filteredItems.Add(itemId);
            }

            _currentPage = 0;
            _totalPages = Math.Max(1, (_filteredItems.Count + ItemsPerPage - 1) / ItemsPerPage);
            UpdateGrid();
            UpdatePageText();
        }

        private void UpdateGrid()
        {
            int start = _currentPage * ItemsPerPage;
            int count = Math.Min(ItemsPerPage, Math.Max(0, _filteredItems.Count - start));
            var pageItems = count > 0 ? _filteredItems.GetRange(start, count) : new List<int>();
            _itemGrid?.SetItems(pageItems, _selectedItemId);
        }

        private void UpdatePageText()
        {
            _pageText?.SetText($"Page {_currentPage + 1}/{_totalPages}");
        }

        private void ChangePage(int delta)
        {
            int newPage = _currentPage + delta;
            if (newPage >= 0 && newPage < _totalPages)
            {
                _currentPage = newPage;
                UpdateGrid();
                UpdatePageText();
            }
        }

        private void OnItemSelected(int itemId)
        {
            _selectedItemId = itemId;
            UpdateGrid();

            if (_lastScannedItems != null && RecipeGraphSystem.Graph != null)
            {
                var tree = RecipeAnalyzer.BuildRecipeTree(itemId, 1, RecipeGraphSystem.Graph, _lastScannedItems);
                _recipeTree?.SetTree(tree, RecipeGraphSystem.Graph, _lastScannedItems);
            }
        }

        public static FilterCategory GetItemCategory(int itemId)
        {
            var item = new Item();
            item.SetDefaults(itemId);

            if (item.damage > 0) return FilterCategory.Weapons;
            if (item.headSlot > 0 || item.bodySlot > 0 || item.legSlot > 0) return FilterCategory.Armor;
            if (item.accessory) return FilterCategory.Accessories;
            if (item.potion || item.buffType > 0) return FilterCategory.Potions;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return FilterCategory.Tools;
            return FilterCategory.Misc;
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
