using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using SteroidGuide.Common;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace SteroidGuide.Common.UI
{
    public enum SortCriteria
    {
        Rarity,
        Name
    }

    public partial class CraftableUIState : UIState
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
        private List<int> _filteredItems = new();
        private int _currentPage;
        private int _totalPages = 1;
        private int _selectedItemId = -1;
        private int _updateCounter;
        private bool _analysisPending;
        private int _analysisDebounceTimer;
        private const int AnalysisDebounceFrames = 90;
        private Task<AnalysisResult> _pendingAnalysisTask;
        private CancellationTokenSource _analysisCts;
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
                ModContent.GetInstance<CraftableUISystem>()?.HideUI();
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

        public void CancelPendingAnalysis()
        {
            _analysisCts?.Cancel();
            _analysisCts?.Dispose();
            _analysisCts = null;
            _pendingAnalysisTask = null;
        }

        public void OnShow()
        {
            CancelPendingAnalysis();
            _latestScanResult = null;
            _analysisResult = null;
            _selectedItemId = -1;
            _currentPage = 0;
            _currentFilter = FilterCategory.All;
            _searchQuery = string.Empty;
            SetSortDropdownOpen(false);
            _sortButton?.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            _analysisPending = false;
            _analysisDebounceTimer = 0;
            _searchTextBox?.Reset();
            _recipeTree?.ClearTree();
            UpdateFilterSelectionStates();
            UpdateNearbyChestStatusText(0);
            RunAnalysis();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _updateCounter++;
            if (_updateCounter % 30 == 0 && Main.LocalPlayer != null)
            {
                var scanResult = ItemScanner.ScanAvailableItems(Main.LocalPlayer);
                if (HasScanChanged(scanResult))
                {
                    _latestScanResult = scanResult;
                    UpdateNearbyChestStatusText(scanResult.ChestCount);
                    _analysisPending = true;
                    _analysisDebounceTimer = AnalysisDebounceFrames;
                }
            }

            if (_analysisPending)
            {
                _analysisDebounceTimer--;
                if (_analysisDebounceTimer <= 0)
                {
                    _analysisPending = false;
                    RunAnalysisFromLatestScan();
                }
            }

            if (_pendingAnalysisTask != null && _pendingAnalysisTask.IsCompleted)
            {
                var task = _pendingAnalysisTask;
                _pendingAnalysisTask = null;
                var system = ModContent.GetInstance<CraftableUISystem>();
                bool isVisible = system?.IsVisible ?? false;
                if (isVisible && task.Status == TaskStatus.RanToCompletion)
                {
                    _analysisResult = task.Result;
                    RebuildItemPropsCache();
                    ApplyFilter();
                }
                else if (task.IsFaulted && task.Exception != null)
                {
                    ModContent.GetInstance<SteroidGuideMod>()?.Logger
                        .Error("Craftable analysis task faulted", task.Exception.GetBaseException());
                }
                else
                {
                    _ = task.Exception; // observe to prevent UnobservedTaskException
                }
            }

            if (IsMouseOverMainPanel)
            {
                Main.LocalPlayer.mouseInterface = true;
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
    }
}
