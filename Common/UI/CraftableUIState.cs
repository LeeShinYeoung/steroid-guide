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
        private const string NearbyChestStatusSyncingKey = "Mods.SteroidGuide.UI.NearbyChestStatusSyncing";
        private const string NearbyChestStatusSyncingFallback = "Referencing {0} nearby chests (syncing {1}/{0})";
        private const string NearbyChestStatusAnalyzingKey = "Mods.SteroidGuide.UI.NearbyChestStatusAnalyzing";
        private const string NearbyChestStatusAnalyzingFallback = "Referencing {0} nearby chests · analyzing...";
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
        private UITitleBar _titleBar;
        private UIText _nearbyChestStatusText;
        private string _lastStatusText;

        // Filter
        private FilterCategory _currentFilter = FilterCategory.All;
        private readonly Dictionary<FilterCategory, UICategoryRow> _filterButtons = new();

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
        private readonly Dictionary<int, string> _ingredientNameCache = new();
        private List<int> _filteredItems = new();
        private int _currentPage;
        private int _totalPages = 1;
        private int _selectedItemId = -1;
        private int _updateCounter;
        private Task<AnalysisResult> _pendingAnalysisTask;
        private CancellationTokenSource _analysisCts;
        private int ItemsPerPage => _itemGrid?.ItemsPerPage ?? 18;

        // Layout — new 3-column redesign
        private const float MainPanelWidth = 1000f;
        private const float MainPanelHeight = 750f;
        private const float TitleBarHeight = 38f;
        private const float CategoryColumnWidth = 150f;
        private const float GridColumnWidth = 350f;
        private const float ColumnDividerWidth = 1f;
        private const float ColumnInnerPadding = 6f;
        private const float SearchBoxHeight = 30f;
        private const float PaginationHeight = 24f;
        private const float PaginationArrowWidth = 26f;
        private const float PaginationArrowHeight = 22f;
        private const float PaginationTextGap = 10f;
        private const float PaginationTextScale = 0.72f;
        private const float CategoryRowHeight = 26f;
        private const float CategoryRowSpacing = 1f;
        private const float CategorySectionTop = 8f;
        private const float CategoryFooterHeight = 34f;
        private const float CategoryFooterPadding = 6f;

        public bool IsSearchFocused => _searchTextBox?.IsFocused ?? false;
        public bool IsMouseOverMainPanel => _mainPanel?.ContainsPoint(Main.MouseScreen) ?? false;

        public override void OnInitialize()
        {
            _mainPanel = new UIPanel();
            _mainPanel.Width.Set(MainPanelWidth, 0f);
            _mainPanel.Height.Set(MainPanelHeight, 0f);
            _mainPanel.HAlign = 0.5f;
            _mainPanel.VAlign = 0.5f;
            _mainPanel.SetPadding(0f);
            _mainPanel.BackgroundColor = UIPalette.PanelBg;
            _mainPanel.BorderColor = UIPalette.WindowBorder;

            BuildTitleBar();
            BuildColumns();

            Append(_mainPanel);
        }

        private void BuildTitleBar()
        {
            _titleBar = new UITitleBar();
            _titleBar.Top.Set(0f, 0f);
            _titleBar.Left.Set(0f, 0f);
            _titleBar.Width.Set(0f, 1f);
            _titleBar.Height.Set(TitleBarHeight, 0f);
            _mainPanel.Append(_titleBar);

            // Status pill on the left
            var statusPill = new UITitleBarStatusPill();
            statusPill.Top.Set(7f, 0f);
            statusPill.Left.Set(10f, 0f);
            statusPill.Width.Set(320f, 0f);
            statusPill.Height.Set(TitleBarHeight - 14f, 0f);
            _titleBar.Append(statusPill);

            _nearbyChestStatusText = new UIText(
                ResolveNearbyChestStatusText(0, 0, NearbyChestStatus.Idle),
                0.75f);
            _nearbyChestStatusText.Top.Set(4f, 0f);
            _nearbyChestStatusText.Left.Set(8f, 0f);
            _nearbyChestStatusText.TextColor = UIPalette.TitleBarStatusText;
            statusPill.Append(_nearbyChestStatusText);

            // Close button on the right
            var closeButton = new UICloseButton();
            closeButton.Top.Set(8f, 0f);
            closeButton.Left.Set(-30f, 1f);
            closeButton.OnLeftClick += (evt, el) =>
            {
                ModContent.GetInstance<CraftableUISystem>()?.HideUI();
            };
            _titleBar.Append(closeButton);
        }

        private void BuildColumns()
        {
            float bodyTop = TitleBarHeight;
            float bodyHeight = MainPanelHeight - TitleBarHeight;

            // Category column
            var categoryColumn = new UIElement();
            categoryColumn.Top.Set(bodyTop, 0f);
            categoryColumn.Left.Set(0f, 0f);
            categoryColumn.Width.Set(CategoryColumnWidth, 0f);
            categoryColumn.Height.Set(bodyHeight, 0f);
            _mainPanel.Append(categoryColumn);
            BuildCategoryColumn(categoryColumn);

            // Divider 1
            var divider1 = new UIColumnDivider();
            divider1.Top.Set(bodyTop, 0f);
            divider1.Left.Set(CategoryColumnWidth, 0f);
            divider1.Width.Set(ColumnDividerWidth, 0f);
            divider1.Height.Set(bodyHeight, 0f);
            _mainPanel.Append(divider1);

            // Grid column
            float gridColumnLeft = CategoryColumnWidth + ColumnDividerWidth;
            var gridColumn = new UIElement();
            gridColumn.Top.Set(bodyTop, 0f);
            gridColumn.Left.Set(gridColumnLeft, 0f);
            gridColumn.Width.Set(GridColumnWidth, 0f);
            gridColumn.Height.Set(bodyHeight, 0f);
            _mainPanel.Append(gridColumn);
            BuildGridColumn(gridColumn);

            // Divider 2
            float divider2Left = gridColumnLeft + GridColumnWidth;
            var divider2 = new UIColumnDivider();
            divider2.Top.Set(bodyTop, 0f);
            divider2.Left.Set(divider2Left, 0f);
            divider2.Width.Set(ColumnDividerWidth, 0f);
            divider2.Height.Set(bodyHeight, 0f);
            _mainPanel.Append(divider2);

            // Recipe column (flex)
            float recipeColumnLeft = divider2Left + ColumnDividerWidth;
            float recipeColumnWidth = MainPanelWidth - recipeColumnLeft;
            var recipeColumn = new UIElement();
            recipeColumn.Top.Set(bodyTop, 0f);
            recipeColumn.Left.Set(recipeColumnLeft, 0f);
            recipeColumn.Width.Set(recipeColumnWidth, 0f);
            recipeColumn.Height.Set(bodyHeight, 0f);
            _mainPanel.Append(recipeColumn);
            BuildRecipeColumn(recipeColumn);
        }

        private void BuildCategoryColumn(UIElement column)
        {
            var bg = new UIColorBackdrop(UIPalette.CatColumnBg);
            bg.Width.Set(0f, 1f);
            bg.Height.Set(0f, 1f);
            column.Append(bg);

            // Category rows stack
            int sortOptionCount = Enum.GetValues(typeof(SortCriteria)).Length;
            float sortDropdownHeight = sortOptionCount * CategoryRowHeight;

            float filterY = CategorySectionTop;
            foreach (var filterDefinition in FilterDefinitions)
            {
                var row = new UICategoryRow(
                    ResolveLocalizedText(filterDefinition.LabelKey, filterDefinition.FallbackLabel));
                row.Top.Set(filterY, 0f);
                row.Left.Set(0f, 0f);
                row.Width.Set(0f, 1f);
                row.Height.Set(CategoryRowHeight, 0f);
                var captured = filterDefinition.Category;
                row.OnLeftClick += (evt, el) => SetFilter(captured);
                row.SetSelected(filterDefinition.Category == _currentFilter);
                column.Append(row);
                _filterButtons[filterDefinition.Category] = row;
                filterY += CategoryRowHeight + CategoryRowSpacing;
            }

            // Rarity filter / sort button in the footer
            _sortButton = new UISortButton();
            _sortButton.Top.Set(-CategoryFooterHeight - CategoryFooterPadding, 1f);
            _sortButton.Left.Set(CategoryFooterPadding, 0f);
            _sortButton.Width.Set(-(CategoryFooterPadding * 2f), 1f);
            _sortButton.Height.Set(CategoryFooterHeight, 0f);
            _sortButton.OnLeftClick += (evt, el) => ToggleSortDropdown();
            _sortButton.SetState(GetSortLabel(_currentSort), _sortDropdownOpen);
            column.Append(_sortButton);

            // Dropdown (appended to _mainPanel only when open, positioned above the sort button)
            _sortDropdownPanel = new UIElement();
            _sortDropdownPanel.Width.Set(CategoryColumnWidth - CategoryFooterPadding * 2f, 0f);
            _sortDropdownPanel.Height.Set(sortDropdownHeight, 0f);
            // Position: above the category-footer button, anchored to the panel.
            _sortDropdownPanel.Top.Set(
                TitleBarHeight + (MainPanelHeight - TitleBarHeight) - CategoryFooterHeight
                - CategoryFooterPadding - sortDropdownHeight - 2f, 0f);
            _sortDropdownPanel.Left.Set(CategoryFooterPadding, 0f);

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
                sortY += CategoryRowHeight;
            }
        }

        private void BuildGridColumn(UIElement column)
        {
            var bg = new UIColorBackdrop(UIPalette.GridColumnBg);
            bg.Width.Set(0f, 1f);
            bg.Height.Set(0f, 1f);
            column.Append(bg);

            float searchTop = ColumnInnerPadding;
            float gridTop = searchTop + SearchBoxHeight + ColumnInnerPadding;
            float paginationTop = (MainPanelHeight - TitleBarHeight) - PaginationHeight - ColumnInnerPadding;
            float gridHeight = paginationTop - gridTop - ColumnInnerPadding;

            _searchTextBox = new UISearchTextBox(
                ResolveLocalizedText(SearchPlaceholderKey, SearchPlaceholderFallback));
            _searchTextBox.Top.Set(searchTop, 0f);
            _searchTextBox.Left.Set(ColumnInnerPadding, 0f);
            _searchTextBox.Width.Set(-ColumnInnerPadding * 2f, 1f);
            _searchTextBox.Height.Set(SearchBoxHeight, 0f);
            _searchTextBox.OnTextChanged += OnSearchTextChanged;
            column.Append(_searchTextBox);

            _itemGrid = new UIItemGrid();
            _itemGrid.Top.Set(gridTop, 0f);
            _itemGrid.Left.Set(ColumnInnerPadding, 0f);
            _itemGrid.Width.Set(-ColumnInnerPadding * 2f, 1f);
            _itemGrid.Height.Set(gridHeight, 0f);
            _itemGrid.OnItemSelected += OnItemSelected;
            _itemGrid.OnPageScrollRequested += TryChangePageFromScroll;
            column.Append(_itemGrid);

            _paginationRow = new UIElement();
            _paginationRow.Top.Set(paginationTop, 0f);
            _paginationRow.Left.Set(ColumnInnerPadding, 0f);
            _paginationRow.Width.Set(-ColumnInnerPadding * 2f, 1f);
            _paginationRow.Height.Set(PaginationHeight, 0f);
            column.Append(_paginationRow);

            _previousPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Left);
            _previousPageButton.Width.Set(PaginationArrowWidth, 0f);
            _previousPageButton.Height.Set(PaginationArrowHeight, 0f);
            _previousPageButton.Top.Set((PaginationHeight - PaginationArrowHeight) * 0.5f, 0f);
            _previousPageButton.OnLeftClick += (evt, el) => ChangePage(-1);
            _paginationRow.Append(_previousPageButton);

            _pageText = new UICenteredText("Page 1/1", PaginationTextScale);
            _pageText.SetColor(UIPalette.PageText);
            _paginationRow.Append(_pageText);

            _nextPageButton = new UIPaginationArrowButton(PaginationArrowDirection.Right);
            _nextPageButton.Width.Set(PaginationArrowWidth, 0f);
            _nextPageButton.Height.Set(PaginationArrowHeight, 0f);
            _nextPageButton.Top.Set((PaginationHeight - PaginationArrowHeight) * 0.5f, 0f);
            _nextPageButton.OnLeftClick += (evt, el) => ChangePage(1);
            _paginationRow.Append(_nextPageButton);

            UpdatePageText();
        }

        private void BuildRecipeColumn(UIElement column)
        {
            var bg = new UIColorBackdrop(UIPalette.RecipeColumnBg);
            bg.Width.Set(0f, 1f);
            bg.Height.Set(0f, 1f);
            column.Append(bg);

            _recipeTree = new UIRecipeTree();
            _recipeTree.Top.Set(0f, 0f);
            _recipeTree.Left.Set(0f, 0f);
            _recipeTree.Width.Set(0f, 1f);
            _recipeTree.Height.Set(0f, 1f);
            _recipeTree.SetHaveLookup(GetScanHaveCount);
            column.Append(_recipeTree);
        }

        private int GetScanHaveCount(int itemId)
        {
            if (!_latestScanResult.HasValue)
                return 0;
            var items = _latestScanResult.Value.Items;
            if (items == null)
                return 0;
            return items.TryGetValue(itemId, out int count) ? count : 0;
        }

        private void UpdateCategoryBadges()
        {
            int categoryCount = Enum.GetValues(typeof(FilterCategory)).Length;
            Span<int> counts = stackalloc int[categoryCount];
            int total = 0;

            if (_analysisResult != null)
            {
                foreach (int itemId in _analysisResult.TopTierItems)
                {
                    if (!_itemPropsCache.TryGetValue(itemId, out var props))
                        continue;
                    int idx = (int)props.Category;
                    if (idx >= 0 && idx < counts.Length)
                        counts[idx]++;
                    total++;
                }
            }

            foreach (var (cat, row) in _filterButtons)
            {
                int value = cat == FilterCategory.All ? total : counts[(int)cat];
                row.SetBadgeCount(value);
            }
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
            _searchTextBox?.Reset();
            _recipeTree?.ClearTree();
            UpdateFilterSelectionStates();
            _lastStatusText = null;
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
                    ApplyFilterPreservingPage();
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

            RefreshNearbyChestStatusText();

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
