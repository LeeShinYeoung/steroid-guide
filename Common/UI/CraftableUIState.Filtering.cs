using System;
using System.Collections.Generic;
using SteroidGuide.Common;
using Terraria;
using Terraria.Localization;

namespace SteroidGuide.Common.UI
{
    public partial class CraftableUIState
    {
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
            return sort.ToString();
        }

        private void OnSearchTextChanged(string query)
        {
            _searchQuery = query ?? string.Empty;
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
                        int cmp = propsB.RarityScore.CompareTo(propsA.RarityScore);
                        return cmp != 0 ? cmp : a.CompareTo(b);
                    }
                    case SortCriteria.Name:
                    {
                        int cmp = string.Compare(propsA.NormalizedName, propsB.NormalizedName, StringComparison.Ordinal);
                        return cmp != 0 ? cmp : a.CompareTo(b);
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
                var tree = CraftableAnalyzer.BuildRecipeTree(
                    itemId,
                    1,
                    RecipeGraphSystem.Graph,
                    _latestScanResult.Value.Items,
                    ignoreOwnedForCurrentNode: true);
                _recipeTree?.SetTree(tree);
            }
        }

        private enum NearbyChestStatus { Idle, Syncing, Waiting, Analyzing }

        private void RefreshNearbyChestStatusText()
        {
            int chestCount = _latestScanResult?.ChestCount ?? 0;
            int syncedCount = _latestScanResult?.SyncedChestCount ?? 0;
            var status = DetermineNearbyChestStatus(chestCount, syncedCount);
            string text = ResolveNearbyChestStatusText(chestCount, syncedCount, status);
            if (text != _lastStatusText)
            {
                _nearbyChestStatusText?.SetText(text);
                _lastStatusText = text;
            }
        }

        private NearbyChestStatus DetermineNearbyChestStatus(int chestCount, int syncedCount)
        {
            if (_pendingAnalysisTask != null) return NearbyChestStatus.Analyzing;
            if (chestCount > 0 && syncedCount < chestCount) return NearbyChestStatus.Syncing;
            if (_analysisPending) return NearbyChestStatus.Waiting;
            return NearbyChestStatus.Idle;
        }

        private static string ResolveNearbyChestStatusText(int chestCount, int syncedCount, NearbyChestStatus status)
        {
            switch (status)
            {
                case NearbyChestStatus.Analyzing:
                    return ResolveLocalizedText(NearbyChestStatusAnalyzingKey, NearbyChestStatusAnalyzingFallback, chestCount);
                case NearbyChestStatus.Syncing:
                    return ResolveLocalizedText(NearbyChestStatusSyncingKey, NearbyChestStatusSyncingFallback, chestCount, syncedCount);
                case NearbyChestStatus.Waiting:
                    return ResolveLocalizedText(NearbyChestStatusWaitingKey, NearbyChestStatusWaitingFallback, chestCount);
                default:
                    string key = chestCount == 1 ? NearbyChestStatusSingularKey : NearbyChestStatusPluralKey;
                    string fallback = chestCount == 1 ? NearbyChestStatusSingularFallback : NearbyChestStatusPluralFallback;
                    return ResolveLocalizedText(key, fallback, chestCount);
            }
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
    }
}
