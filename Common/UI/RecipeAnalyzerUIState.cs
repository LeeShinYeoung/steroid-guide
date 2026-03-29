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

    public enum SortCriteria
    {
        Rarity,
        Name,
        Value,
        RecipeDepth
    }

    public class RecipeAnalyzerUIState : UIState
    {
        private UIPanel _mainPanel;

        // Filter
        private FilterCategory _currentFilter = FilterCategory.All;
        private readonly Dictionary<FilterCategory, UIText> _filterButtons = new();

        // Sort
        private SortCriteria _currentSort = SortCriteria.Rarity;
        private UIText _sortButton;
        private UIPanel _sortDropdownPanel;
        private readonly Dictionary<SortCriteria, UIText> _sortOptions = new();
        private bool _sortDropdownOpen;

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
        private int ItemsPerPage => _itemGrid?.ItemsPerPage ?? 20;

        public override void OnInitialize()
        {
            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(820f, 0f);
            _mainPanel.Height.Set(600f, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.SetPadding(12f);

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

            // ── Sort dropdown (below filter sidebar) ──
            _sortButton = new UIText($"Sort: {_currentSort} \u25BC", 0.7f);
            _sortButton.Top.Set(268f, 0f);
            _sortButton.Left.Set(6f, 0f);
            _sortButton.Width.Set(108f, 0f);
            _sortButton.OnLeftClick += (evt, el) => ToggleSortDropdown();
            _mainPanel.Append(_sortButton);

            _sortDropdownPanel = new UIPanel();
            _sortDropdownPanel.Top.Set(290f, 0f);
            _sortDropdownPanel.Left.Set(0f, 0f);
            _sortDropdownPanel.Width.Set(120f, 0f);
            _sortDropdownPanel.Height.Set(112f, 0f);
            _sortDropdownPanel.SetPadding(6f);

            float sortY = 0f;
            foreach (SortCriteria sort in Enum.GetValues(typeof(SortCriteria)))
            {
                string label = sort == SortCriteria.RecipeDepth ? "Recipe Depth" : sort.ToString();
                string prefix = sort == _currentSort ? "[*]" : "[ ]";
                var option = new UIText($"{prefix} {label}", 0.7f);
                option.Top.Set(sortY, 0f);
                var captured = sort;
                option.OnLeftClick += (evt, el) => SelectSort(captured);
                _sortDropdownPanel.Append(option);
                _sortOptions[sort] = option;
                sortY += 26f;
            }
            // Start hidden
            // _sortDropdownPanel is only appended when dropdown is open

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
                var scanResult = ItemScanner.ScanAvailableItems(Main.LocalPlayer);
                if (!DictEquals(_lastScannedItems, scanResult.Items))
                {
                    _lastScannedItems = scanResult.Items;
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
            var initialScan = ItemScanner.ScanAvailableItems(Main.LocalPlayer);
            _lastScannedItems = initialScan.Items;
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

        private void ToggleSortDropdown()
        {
            _sortDropdownOpen = !_sortDropdownOpen;
            if (_sortDropdownOpen)
                _mainPanel.Append(_sortDropdownPanel);
            else
                _mainPanel.RemoveChild(_sortDropdownPanel);
        }

        private void SelectSort(SortCriteria sort)
        {
            _currentSort = sort;
            _sortButton.SetText($"Sort: {(sort == SortCriteria.RecipeDepth ? "Recipe Depth" : sort.ToString())} \u25BC");
            foreach (var (s, option) in _sortOptions)
            {
                string label = s == SortCriteria.RecipeDepth ? "Recipe Depth" : s.ToString();
                string prefix = s == _currentSort ? "[*]" : "[ ]";
                option.SetText($"{prefix} {label}");
            }
            // Close dropdown
            if (_sortDropdownOpen)
            {
                _sortDropdownOpen = false;
                _mainPanel.RemoveChild(_sortDropdownPanel);
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

            // Apply sorting
            _filteredItems.Sort((a, b) =>
            {
                switch (_currentSort)
                {
                    case SortCriteria.Rarity:
                    {
                        var itemA = new Item(); itemA.SetDefaults(a);
                        var itemB = new Item(); itemB.SetDefaults(b);
                        int cmp = itemB.rare.CompareTo(itemA.rare);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.Name:
                    {
                        var itemA = new Item(); itemA.SetDefaults(a);
                        var itemB = new Item(); itemB.SetDefaults(b);
                        return string.Compare(itemA.Name, itemB.Name, StringComparison.Ordinal);
                    }
                    case SortCriteria.Value:
                    {
                        var itemA = new Item(); itemA.SetDefaults(a);
                        var itemB = new Item(); itemB.SetDefaults(b);
                        int cmp = itemB.value.CompareTo(itemA.value);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.RecipeDepth:
                    {
                        var graph = RecipeGraphSystem.Graph;
                        int depthA = graph != null ? RecipeAnalyzer.GetRecipeDepth(a, graph) : 0;
                        int depthB = graph != null ? RecipeAnalyzer.GetRecipeDepth(b, graph) : 0;
                        int cmp = depthB.CompareTo(depthA);
                        if (cmp != 0) return cmp;
                        var itemA = new Item(); itemA.SetDefaults(a);
                        var itemB = new Item(); itemB.SetDefaults(b);
                        return itemB.rare.CompareTo(itemA.rare);
                    }
                    default:
                        return 0;
                }
            });

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
