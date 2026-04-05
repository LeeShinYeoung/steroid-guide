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
        private const string NearbyChestStatusSingularKey = "Mods.SteroidGuide.UI.NearbyChestStatusSingular";
        private const string NearbyChestStatusSingularFallback = "Referencing {0} nearby chest";
        private const string NearbyChestStatusPluralKey = "Mods.SteroidGuide.UI.NearbyChestStatusPlural";
        private const string NearbyChestStatusPluralFallback = "Referencing {0} nearby chests";
        private const string SearchPlaceholderKey = "Mods.SteroidGuide.UI.SearchPlaceholder";
        private const string SearchPlaceholderFallback = "Search craftable items...";

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
        private UIText _nearbyChestStatusText;

        // Filter
        private FilterCategory _currentFilter = FilterCategory.All;
        private readonly Dictionary<FilterCategory, UISelectableOption> _filterButtons = new();

        // Sort
        private SortCriteria _currentSort = SortCriteria.Rarity;
        private UISortButton _sortButton;
        private UIElement _sortDropdownPanel;
        private readonly Dictionary<SortCriteria, UISelectableOption> _sortOptions = new();
        private bool _sortDropdownOpen;

        // Search
        private UISearchTextBox _searchTextBox;
        private string _searchQuery = string.Empty;

        // Item grid
        private UIItemGrid _itemGrid;
        private UIElement _paginationRow;
        private UIPaginationArrowButton _previousPageButton;
        private UIPaginationArrowButton _nextPageButton;
        private UICenteredText _pageText;

        // Recipe tree
        private UIRecipeTree _recipeTree;

        // State
        private AnalysisResult _analysisResult;
        private ScanResult? _latestScanResult;
        private readonly Dictionary<int, CachedItemProps> _itemPropsCache = new();
        private readonly Dictionary<int, int> _recipeDepthCache = new();
        private List<int> _filteredItems = new();
        private int _currentPage;
        private int _totalPages = 1;
        private int _selectedItemId = -1;
        private int _updateCounter;
        private int ItemsPerPage => _itemGrid?.ItemsPerPage ?? 20;
        private const float MainPanelWidth = 820f;
        private const float MainPanelHeight = 600f;
        private const float MainPanelPadding = 12f;
        private const float PaginationArrowWidth = 30f;
        private const float PaginationTextGap = 18f;
        private const float PaginationTextScale = 0.75f;
        private const float FilterPanelTop = 42f;
        private const float SidebarRowHeight = 28f;
        private const float FilterOptionGap = 2f;
        private const float FilterOptionStep = SidebarRowHeight + FilterOptionGap;
        private const float SortOptionStep = SidebarRowHeight;
        private const float SidebarPanelWidth = 120f;
        private const float ContentColumnGap = 12f;
        private const float ContentColumnLeft = SidebarPanelWidth + ContentColumnGap;
        private const float ContentColumnWidth = MainPanelWidth - MainPanelPadding * 2f - ContentColumnLeft;
        private const float SearchBoxTop = 42f;
        private const float SearchBoxHeight = 32f;
        private const float ItemGridTop = 80f;
        private const float RecipeTreeTop = 346f;
        private const float RecipeTreeHeight = 228f;

        private static readonly Color SidebarPanelBackgroundColor = new(20, 26, 44, 175);
        private static readonly Color SidebarPanelBorderColor = new(94, 108, 154, 120);
        private static readonly Color SidebarControlBackgroundColor = new(33, 42, 73, 215);
        private static readonly Color SidebarControlBorderColor = new(118, 136, 195, 185);

        public bool IsSearchFocused => _searchTextBox?.IsFocused ?? false;
        public bool IsMouseOverMainPanel => _mainPanel?.ContainsPoint(Main.MouseScreen) ?? false;

        public override void OnInitialize()
        {
            float itemGridHeight = UIItemGrid.GetPreferredHeight(ContentColumnWidth);
            float footerControlsTop = GetFooterControlsTop(itemGridHeight);
            float filterPanelHeight = FilterDefinitions.Length * FilterOptionStep - FilterOptionGap;
            float sortButtonTop = GetSidebarSortButtonTop(filterPanelHeight);
            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(MainPanelWidth, 0f);
            _mainPanel.Height.Set(MainPanelHeight, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.SetPadding(MainPanelPadding);

            var closeButton = new UICloseButton();
            closeButton.Top.Set(2f, 0f);
            closeButton.Left.Set(-34f, 1f);
            closeButton.OnLeftClick += (evt, el) =>
            {
                ModContent.GetInstance<RecipeAnalyzerUISystem>()?.HideUI();
            };
            _mainPanel.Append(closeButton);

            _nearbyChestStatusText = new UIText(ResolveNearbyChestStatusText(0), 0.8f);
            _nearbyChestStatusText.Top.Set(10f, 0f);
            _nearbyChestStatusText.Left.Set(8f, 0f);
            _mainPanel.Append(_nearbyChestStatusText);

            // ── Filter sidebar ──
            int sortOptionCount = Enum.GetValues(typeof(SortCriteria)).Length;
            float sortDropdownHeight = sortOptionCount * SortOptionStep;

            var filterPanel = new UIElement();
            filterPanel.Top.Set(FilterPanelTop, 0f);
            filterPanel.Left.Set(0f, 0f);
            filterPanel.Width.Set(SidebarPanelWidth, 0f);
            filterPanel.Height.Set(filterPanelHeight, 0f);
            _mainPanel.Append(filterPanel);

            float filterY = 0f;
            foreach (var filterDefinition in FilterDefinitions)
            {
                var btn = new UISelectableOption(ResolveLocalizedText(filterDefinition.LabelKey, filterDefinition.FallbackLabel));
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
            _sortButton.Top.Set(sortButtonTop, 0f);
            _sortButton.Left.Set(0f, 0f);
            _sortButton.Width.Set(SidebarPanelWidth, 0f);
            _sortButton.Height.Set(SidebarRowHeight, 0f);
            _sortButton.OnLeftClick += (evt, el) => ToggleSortDropdown();
            _sortButton.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            _mainPanel.Append(_sortButton);

            _sortDropdownPanel = new UIElement();
            _sortDropdownPanel.Top.Set(sortButtonTop + SidebarRowHeight, 0f);
            _sortDropdownPanel.Left.Set(0f, 0f);
            _sortDropdownPanel.Width.Set(SidebarPanelWidth, 0f);
            _sortDropdownPanel.Height.Set(sortDropdownHeight, 0f);

            float sortY = 0f;
            foreach (SortCriteria sort in Enum.GetValues(typeof(SortCriteria)))
            {
                var option = new UISelectableOption(GetSortLabel(sort));
                option.Top.Set(sortY, 0f);
                var captured = sort;
                option.OnLeftClick += (evt, el) => SelectSort(captured);
                option.SetSelected(sort == _currentSort);
                _sortDropdownPanel.Append(option);
                _sortOptions[sort] = option;
                sortY += SortOptionStep;
            }
            // Start hidden
            // _sortDropdownPanel is only appended when dropdown is open

            // ── Search box ──
            _searchTextBox = new UISearchTextBox(
                ResolveLocalizedText(SearchPlaceholderKey, SearchPlaceholderFallback));
            _searchTextBox.Top.Set(SearchBoxTop, 0f);
            _searchTextBox.Left.Set(ContentColumnLeft, 0f);
            _searchTextBox.Width.Set(ContentColumnWidth, 0f);
            _searchTextBox.Height.Set(SearchBoxHeight, 0f);
            _searchTextBox.OnTextChanged += OnSearchTextChanged;
            _mainPanel.Append(_searchTextBox);

            // ── Item grid ──
            _itemGrid = new UIItemGrid();
            _itemGrid.Top.Set(ItemGridTop, 0f);
            _itemGrid.Left.Set(ContentColumnLeft, 0f);
            _itemGrid.Width.Set(ContentColumnWidth, 0f);
            _itemGrid.Height.Set(itemGridHeight, 0f);
            _itemGrid.OnItemSelected += OnItemSelected;
            _itemGrid.OnPageScrollRequested += TryChangePageFromScroll;
            _mainPanel.Append(_itemGrid);

            // ── Pagination ──
            _paginationRow = new UIElement();
            _paginationRow.Top.Set(footerControlsTop, 0f);
            _paginationRow.Left.Set(ContentColumnLeft, 0f);
            _paginationRow.Width.Set(ContentColumnWidth, 0f);
            _paginationRow.Height.Set(SidebarRowHeight, 0f);
            _mainPanel.Append(_paginationRow);

            _previousPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Left);
            _previousPageButton.Width.Set(PaginationArrowWidth, 0f);
            _previousPageButton.Height.Set(SidebarRowHeight, 0f);
            _previousPageButton.Top.Set(0f, 0f);
            _previousPageButton.OnLeftClick += (evt, el) => ChangePage(-1);
            _paginationRow.Append(_previousPageButton);

            _pageText = new UICenteredText("Page 1/1", PaginationTextScale);
            _paginationRow.Append(_pageText);

            _nextPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Right);
            _nextPageButton.Width.Set(PaginationArrowWidth, 0f);
            _nextPageButton.Height.Set(SidebarRowHeight, 0f);
            _nextPageButton.Top.Set(0f, 0f);
            _nextPageButton.OnLeftClick += (evt, el) => ChangePage(1);
            _paginationRow.Append(_nextPageButton);

            UpdatePageText();

            // ── Recipe tree (bottom half) ──
            _recipeTree = new UIRecipeTree();
            _recipeTree.Top.Set(RecipeTreeTop, 0f);
            _recipeTree.Left.Set(0f, 0f);
            _recipeTree.Width.Set(0f, 1f);
            _recipeTree.Height.Set(RecipeTreeHeight, 0f);
            _mainPanel.Append(_recipeTree);

            Append(_mainPanel);
        }

        private static float GetFooterControlsTop(float itemGridHeight)
        {
            float itemGridBottom = ItemGridTop + itemGridHeight;
            float availableGap = RecipeTreeTop - itemGridBottom - SidebarRowHeight;
            return itemGridBottom + availableGap * 0.5f;
        }

        private static float GetSidebarSortButtonTop(float filterPanelHeight)
        {
            float filterBottom = FilterPanelTop + filterPanelHeight;
            float gapHeight = Math.Max(0f, RecipeTreeTop - filterBottom);
            return filterBottom + Math.Max(0f, (gapHeight - SidebarRowHeight) * 0.5f);
        }

        public void OnShow()
        {
            _latestScanResult = null;
            _analysisResult = null;
            _selectedItemId = -1;
            _currentPage = 0;
            _currentFilter = FilterCategory.All;
            _searchQuery = string.Empty;
            SetSortDropdownOpen(false);
            _sortButton?.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            _searchTextBox?.Reset();
            _recipeTree?.ClearTree();
            UpdateFilterSelectionStates();
            UpdateNearbyChestStatusText(0);
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
                if (HasScanChanged(scanResult))
                {
                    ApplyScanResult(scanResult);
                }
            }

            // Block world interaction when hovering our panel
            if (IsMouseOverMainPanel)
            {
                Main.LocalPlayer.mouseInterface = true;
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

            _analysisResult = RecipeAnalyzer.Analyze(graph, scanResult.Items);
            RebuildItemPropsCache();
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
                if (!_itemPropsCache.TryGetValue(itemId, out var props))
                    continue;

                if (_currentFilter != FilterCategory.All && props.Category != _currentFilter)
                    continue;

                if (hasSearchQuery && !props.NormalizedName.Contains(normalizedQuery, StringComparison.Ordinal))
                    continue;

                _filteredItems.Add(itemId);
            }

            // Apply sorting
            _filteredItems.Sort((a, b) =>
            {
                var propsA = _itemPropsCache[a];
                var propsB = _itemPropsCache[b];

                switch (_currentSort)
                {
                    case SortCriteria.Rarity:
                    {
                        int cmp = propsB.Rare.CompareTo(propsA.Rare);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.Name:
                    {
                        int cmp = string.Compare(propsA.NormalizedName, propsB.NormalizedName, StringComparison.Ordinal);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.Value:
                    {
                        int cmp = propsB.Value.CompareTo(propsA.Value);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.RecipeDepth:
                    {
                        _recipeDepthCache.TryGetValue(a, out int depthA);
                        _recipeDepthCache.TryGetValue(b, out int depthB);
                        int cmp = depthB.CompareTo(depthA);
                        return cmp != 0 ? cmp : propsB.Rare.CompareTo(propsA.Rare);
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
                float textWidth = _pageText.MeasuredTextWidth;
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

            if (_latestScanResult.HasValue && _latestScanResult.Value.Items != null && RecipeGraphSystem.Graph != null)
            {
                var tree = RecipeAnalyzer.BuildRecipeTree(
                    itemId,
                    1,
                    RecipeGraphSystem.Graph,
                    _latestScanResult.Value.Items,
                    ignoreOwnedForCurrentNode: true);
                _recipeTree?.SetTree(tree);
            }
        }

        public bool HandleEscapeKey()
        {
            return _searchTextBox?.HandleEscape() ?? false;
        }

        public bool HandleSearchEnterKey()
        {
            return _searchTextBox?.HandleEnter() ?? false;
        }

        public void ApplySearchTextInputCapture()
        {
            _searchTextBox?.ApplyFocusedInputCapture();
        }

        public void UpdateSearchTextInput()
        {
            _searchTextBox?.UpdateFocusedTextInput();
        }

        private void OnSearchTextChanged(string query)
        {
            _searchQuery = query ?? string.Empty;
            ApplyFilter();
        }

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
                    _recipeDepthCache[itemId] = RecipeAnalyzer.GetRecipeDepth(itemId, graph);
            }
        }

        private static string NormalizeSearchText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Trim().ToUpperInvariant();
        }

        private void UpdateNearbyChestStatusText(int chestCount)
        {
            _nearbyChestStatusText?.SetText(ResolveNearbyChestStatusText(chestCount));
        }

        private static string ResolveNearbyChestStatusText(int chestCount)
        {
            string key = chestCount == 1 ? NearbyChestStatusSingularKey : NearbyChestStatusPluralKey;
            string fallback = chestCount == 1 ? NearbyChestStatusSingularFallback : NearbyChestStatusPluralFallback;
            return ResolveLocalizedText(key, fallback, chestCount);
        }

        private static string ResolveLocalizedText(string key, string fallback)
        {
            return ResolveLocalizedText(key, fallback, Array.Empty<object>());
        }

        private static string ResolveLocalizedText(string key, string fallback, params object[] args)
        {
            if (!Language.Exists(key))
            {
                return FormatLocalizedText(fallback, args);
            }

            string resolvedText = Language.GetTextValue(key, args);
            return string.IsNullOrWhiteSpace(resolvedText) || string.Equals(resolvedText, key, StringComparison.Ordinal)
                ? FormatLocalizedText(fallback, args)
                : resolvedText;
        }

        private static string FormatLocalizedText(string text, params object[] args)
        {
            return args == null || args.Length == 0 ? text : string.Format(text, args);
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
