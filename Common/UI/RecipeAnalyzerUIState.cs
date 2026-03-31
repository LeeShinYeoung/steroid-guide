using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SteroidGuide.Common;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public enum SortCriteria
    {
        Rarity,
        Name,
        Value,
        RecipeDepth
    }

    public class RecipeAnalyzerUIState : UIState
    {
        private const string SearchPlaceholderKey = "Mods.SteroidGuide.UI.SearchPlaceholder";
        private const string SearchPlaceholderFallback = "Search craftable items...";
        private const string SearchClearKey = "Mods.SteroidGuide.UI.SearchClear";
        private const string SearchClearFallback = "Clear";

        private static readonly (FilterCategory Category, string LabelKey, string FallbackLabel)[] FilterDefinitions =
        [
            (FilterCategory.All, "Mods.SteroidGuide.UI.Filters.All", "All"),
            (FilterCategory.Weapons, "Mods.SteroidGuide.UI.Filters.Weapons", "Weapons"),
            (FilterCategory.Armor, "Mods.SteroidGuide.UI.Filters.Armor", "Armor"),
            (FilterCategory.Accessories, "Mods.SteroidGuide.UI.Filters.Accessories", "Accessories"),
            (FilterCategory.Tools, "Mods.SteroidGuide.UI.Filters.Tools", "Tools"),
            (FilterCategory.Consumables, "Mods.SteroidGuide.UI.Filters.Consumables", "Consumables"),
            (FilterCategory.Placeables, "Mods.SteroidGuide.UI.Filters.Placeables", "Placeables"),
            (FilterCategory.Materials, "Mods.SteroidGuide.UI.Filters.Materials", "Materials"),
            (FilterCategory.Misc, "Mods.SteroidGuide.UI.Filters.Misc", "Misc")
        ];

        private UIPanel _mainPanel;

        // Filter
        private FilterCategory _currentFilter = FilterCategory.All;
        private readonly Dictionary<FilterCategory, UIFilterOption> _filterButtons = new();

        // Sort
        private SortCriteria _currentSort = SortCriteria.Rarity;
        private UISortButton _sortButton;
        private UIPanel _sortDropdownPanel;
        private readonly Dictionary<SortCriteria, UISortOption> _sortOptions = new();
        private bool _sortDropdownOpen;

        // Search
        private UISearchTextBox _searchTextBox;
        private string _searchQuery = string.Empty;

        // Item grid
        private UIItemGrid _itemGrid;
        private UIElement _paginationRow;
        private UIPaginationArrowButton _previousPageButton;
        private UIPaginationArrowButton _nextPageButton;
        private UIText _pageText;

        // Recipe tree
        private UIRecipeTree _recipeTree;

        // State
        private AnalysisResult _analysisResult;
        private Dictionary<int, int> _lastScannedItems;
        private readonly Dictionary<int, string> _normalizedItemNames = new();
        private List<int> _filteredItems = new();
        private int _currentPage;
        private int _totalPages = 1;
        private int _selectedItemId = -1;
        private int _updateCounter;
        private int ItemsPerPage => _itemGrid?.ItemsPerPage ?? 20;
        private const float PaginationArrowWidth = 30f;
        private const float PaginationTextGap = 18f;
        private const float PaginationTextScale = 0.75f;
        private const float FilterPanelTop = 42f;
        private const float FilterOptionStep = 26f;
        private const float FilterPanelPadding = 6f;
        private const float SidebarPanelWidth = 120f;

        private static readonly Color SidebarPanelBackgroundColor = new(20, 26, 44, 175);
        private static readonly Color SidebarPanelBorderColor = new(94, 108, 154, 120);
        private static readonly Color SidebarControlBackgroundColor = new(33, 42, 73, 215);
        private static readonly Color SidebarControlBorderColor = new(118, 136, 195, 185);

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
            filterPanel.Top.Set(FilterPanelTop, 0f);
            filterPanel.Left.Set(0f, 0f);
            filterPanel.Width.Set(SidebarPanelWidth, 0f);
            filterPanel.Height.Set(FilterDefinitions.Length * FilterOptionStep + FilterPanelPadding, 0f);
            filterPanel.SetPadding(FilterPanelPadding);
            filterPanel.BackgroundColor = SidebarPanelBackgroundColor;
            filterPanel.BorderColor = SidebarPanelBorderColor;
            _mainPanel.Append(filterPanel);

            float filterY = 0f;
            foreach (var filterDefinition in FilterDefinitions)
            {
                var btn = new UIFilterOption(ResolveLocalizedText(filterDefinition.LabelKey, filterDefinition.FallbackLabel));
                btn.Top.Set(filterY, 0f);
                var captured = filterDefinition.Category;
                btn.OnLeftClick += (evt, el) => SetFilter(captured);
                btn.SetSelected(filterDefinition.Category == _currentFilter);
                filterPanel.Append(btn);
                _filterButtons[filterDefinition.Category] = btn;
                filterY += FilterOptionStep;
            }

            // ── Sort dropdown (below filter sidebar) ──
            _sortButton = new UISortButton();
            _sortButton.Top.Set(FilterPanelTop + FilterDefinitions.Length * FilterOptionStep + 12f, 0f);
            _sortButton.Left.Set(0f, 0f);
            _sortButton.Width.Set(SidebarPanelWidth, 0f);
            _sortButton.Height.Set(28f, 0f);
            _sortButton.OnLeftClick += (evt, el) => ToggleSortDropdown();
            _sortButton.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            _mainPanel.Append(_sortButton);

            _sortDropdownPanel = new UIPanel();
            _sortDropdownPanel.Top.Set(FilterPanelTop + FilterDefinitions.Length * FilterOptionStep + 40f, 0f);
            _sortDropdownPanel.Left.Set(0f, 0f);
            _sortDropdownPanel.Width.Set(SidebarPanelWidth, 0f);
            _sortDropdownPanel.Height.Set(112f, 0f);
            _sortDropdownPanel.SetPadding(6f);
            _sortDropdownPanel.BackgroundColor = SidebarPanelBackgroundColor;
            _sortDropdownPanel.BorderColor = SidebarPanelBorderColor;

            float sortY = 0f;
            foreach (SortCriteria sort in Enum.GetValues(typeof(SortCriteria)))
            {
                var option = new UISortOption(GetSortLabel(sort));
                option.Top.Set(sortY, 0f);
                var captured = sort;
                option.OnLeftClick += (evt, el) => SelectSort(captured);
                option.SetSelected(sort == _currentSort);
                _sortDropdownPanel.Append(option);
                _sortOptions[sort] = option;
                sortY += 24f;
            }
            // Start hidden
            // _sortDropdownPanel is only appended when dropdown is open

            // ── Search box ──
            _searchTextBox = new UISearchTextBox(
                ResolveLocalizedText(SearchPlaceholderKey, SearchPlaceholderFallback),
                ResolveLocalizedText(SearchClearKey, SearchClearFallback));
            _searchTextBox.Top.Set(42f, 0f);
            _searchTextBox.Left.Set(132f, 0f);
            _searchTextBox.Width.Set(650f, 0f);
            _searchTextBox.Height.Set(32f, 0f);
            _searchTextBox.OnTextChanged += OnSearchTextChanged;
            _mainPanel.Append(_searchTextBox);

            // ── Item grid ──
            _itemGrid = new UIItemGrid();
            _itemGrid.Top.Set(80f, 0f);
            _itemGrid.Left.Set(132f, 0f);
            _itemGrid.Width.Set(650f, 0f);
            _itemGrid.Height.Set(226f, 0f);
            _itemGrid.OnItemSelected += OnItemSelected;
            _itemGrid.OnPageScrollRequested += TryChangePageFromScroll;
            _mainPanel.Append(_itemGrid);

            // ── Pagination ──
            _paginationRow = new UIElement();
            _paginationRow.Top.Set(312f, 0f);
            _paginationRow.Left.Set(132f, 0f);
            _paginationRow.Width.Set(650f, 0f);
            _paginationRow.Height.Set(26f, 0f);
            _mainPanel.Append(_paginationRow);

            _previousPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Left);
            _previousPageButton.Width.Set(PaginationArrowWidth, 0f);
            _previousPageButton.Height.Set(24f, 0f);
            _previousPageButton.Top.Set(1f, 0f);
            _previousPageButton.OnLeftClick += (evt, el) => ChangePage(-1);
            _paginationRow.Append(_previousPageButton);

            _pageText = new UIText("Page 1/1", PaginationTextScale);
            _pageText.Top.Set(3f, 0f);
            _paginationRow.Append(_pageText);

            _nextPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Right);
            _nextPageButton.Width.Set(PaginationArrowWidth, 0f);
            _nextPageButton.Height.Set(24f, 0f);
            _nextPageButton.Top.Set(1f, 0f);
            _nextPageButton.OnLeftClick += (evt, el) => ChangePage(1);
            _paginationRow.Append(_nextPageButton);

            UpdatePageText();

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
            _currentFilter = FilterCategory.All;
            _searchQuery = string.Empty;
            SetSortDropdownOpen(false);
            _sortButton?.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            _searchTextBox?.Reset();
            _recipeTree?.ClearTree();
            UpdateFilterSelectionStates();
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
            RebuildItemNameCache();
            ApplyFilter();
        }

        private void SetFilter(FilterCategory category)
        {
            _currentFilter = category;
            UpdateFilterSelectionStates();
            ApplyFilter();
        }

        private void UpdateFilterSelectionStates()
        {
            foreach (var (cat, btn) in _filterButtons)
            {
                btn.SetSelected(cat == _currentFilter);
            }
        }

        private void ToggleSortDropdown()
        {
            SetSortDropdownOpen(!_sortDropdownOpen);
        }

        private void SelectSort(SortCriteria sort)
        {
            _currentSort = sort;
            _sortButton?.SetState(GetSortLabel(sort), _sortDropdownOpen);
            foreach (var (s, option) in _sortOptions)
            {
                option.SetSelected(s == _currentSort);
            }

            SetSortDropdownOpen(false);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_analysisResult == null) return;

            string normalizedQuery = NormalizeSearchText(_searchQuery);
            bool hasSearchQuery = normalizedQuery.Length > 0;

            _filteredItems.Clear();
            foreach (int itemId in _analysisResult.TopTierItems)
            {
                if (_currentFilter != FilterCategory.All && ItemCategoryClassifier.Classify(itemId) != _currentFilter)
                    continue;

                if (hasSearchQuery && (!_normalizedItemNames.TryGetValue(itemId, out string normalizedName) ||
                    !normalizedName.Contains(normalizedQuery, StringComparison.Ordinal)))
                {
                    continue;
                }

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
                        _normalizedItemNames.TryGetValue(a, out string nameA);
                        _normalizedItemNames.TryGetValue(b, out string nameB);
                        int cmp = string.Compare(nameA, nameB, StringComparison.Ordinal);
                        return cmp != 0 ? cmp : a.CompareTo(b);
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

            if (_selectedItemId != -1 && !_filteredItems.Contains(_selectedItemId))
            {
                _selectedItemId = -1;
                _recipeTree?.ClearTree();
            }

            _currentPage = 0;
            _totalPages = Math.Max(1, (_filteredItems.Count + ItemsPerPage - 1) / ItemsPerPage);
            string emptyStateKey = hasSearchQuery
                ? "Mods.SteroidGuide.UI.SearchNoResults"
                : _currentFilter == FilterCategory.All
                    ? "Mods.SteroidGuide.UI.NoCraftableItems"
                    : "Mods.SteroidGuide.UI.NoItemsInCategory";
            _itemGrid?.SetEmptyStateText(Language.GetTextValue(emptyStateKey));
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
            string pageLabel = $"Page {_currentPage + 1}/{_totalPages}";
            _pageText?.SetText(pageLabel);

            if (_paginationRow != null && _pageText != null)
            {
                float textWidth = FontAssets.MouseText.Value.MeasureString(pageLabel).X * PaginationTextScale;
                _pageText.Left.Set(-textWidth * 0.5f, 0.5f);
                _previousPageButton?.Left.Set(-(textWidth * 0.5f + PaginationTextGap + PaginationArrowWidth), 0.5f);
                _nextPageButton?.Left.Set(textWidth * 0.5f + PaginationTextGap, 0.5f);
                _paginationRow.Recalculate();
            }

            UpdatePaginationButtonState();
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

        private void TryChangePageFromScroll(int scrollDelta)
        {
            if (scrollDelta == 0 || _totalPages <= 1)
            {
                return;
            }

            int pageDelta = scrollDelta > 0 ? -1 : 1;
            int requestedPage = _currentPage + pageDelta;
            if (requestedPage < 0 || requestedPage >= _totalPages)
            {
                return;
            }

            ChangePage(pageDelta);
        }

        private void UpdatePaginationButtonState()
        {
            bool canGoPrevious = _currentPage > 0;
            bool canGoNext = _currentPage < _totalPages - 1;

            if (_previousPageButton != null)
            {
                _previousPageButton.IsEnabled = canGoPrevious;
                _previousPageButton.IgnoresMouseInteraction = !canGoPrevious;
            }

            if (_nextPageButton != null)
            {
                _nextPageButton.IsEnabled = canGoNext;
                _nextPageButton.IgnoresMouseInteraction = !canGoNext;
            }
        }

        private void OnItemSelected(int itemId)
        {
            _selectedItemId = itemId;
            UpdateGrid();

            if (_lastScannedItems != null && RecipeGraphSystem.Graph != null)
            {
                var tree = RecipeAnalyzer.BuildRecipeTree(
                    itemId,
                    1,
                    RecipeGraphSystem.Graph,
                    _lastScannedItems,
                    ignoreOwnedForCurrentNode: true);
                _recipeTree?.SetTree(tree, RecipeGraphSystem.Graph, _lastScannedItems);
            }
        }

        public bool HandleEscapeKey()
        {
            return _searchTextBox?.HandleEscape() ?? false;
        }

        private void OnSearchTextChanged(string query)
        {
            _searchQuery = query ?? string.Empty;
            ApplyFilter();
        }

        private void RebuildItemNameCache()
        {
            _normalizedItemNames.Clear();
            if (_analysisResult == null)
            {
                return;
            }

            foreach (int itemId in _analysisResult.TopTierItems)
            {
                var item = new Item();
                item.SetDefaults(itemId);
                _normalizedItemNames[itemId] = NormalizeSearchText(item.Name);
            }
        }

        private static string NormalizeSearchText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToUpperInvariant();
        }

        private static string ResolveLocalizedText(string key, string fallback)
        {
            if (!Language.Exists(key))
            {
                return fallback;
            }

            string resolvedText = Language.GetTextValue(key);
            return string.IsNullOrWhiteSpace(resolvedText) || string.Equals(resolvedText, key, StringComparison.Ordinal)
                ? fallback
                : resolvedText;
        }

        private void SetSortDropdownOpen(bool open)
        {
            _sortDropdownOpen = open;
            _sortButton?.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);

            if (_sortDropdownPanel == null || _mainPanel == null)
            {
                return;
            }

            if (_sortDropdownOpen)
            {
                if (_sortDropdownPanel.Parent == null)
                {
                    _mainPanel.Append(_sortDropdownPanel);
                }
            }
            else if (_sortDropdownPanel.Parent != null)
            {
                _mainPanel.RemoveChild(_sortDropdownPanel);
            }
        }

        private static string GetSortLabel(SortCriteria sort)
        {
            return sort == SortCriteria.RecipeDepth ? "Recipe Depth" : sort.ToString();
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
