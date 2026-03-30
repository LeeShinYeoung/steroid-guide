# Build Report

## Implemented
- [x] Added a keyword search field to the analyzer UI that filters only the top-tier craftable item grid.
- [x] Composed keyword filtering with the existing category filter, sort order, pagination, and stale selection cleanup.
- [x] Added search-specific localization and empty-state messaging without changing the recipe analysis pipeline.

## Files Changed
- `Common/UI/UISearchTextBox.cs` — added a focused search input element with Terraria text capture and clear affordance.
- `Common/UI/RecipeAnalyzerUIState.cs` — integrated search state, cached localized names, composed filtering, reset pagination, and cleared filtered-out selections.
- `Common/UI/RecipeAnalyzerUISystem.cs` — routed `Escape` to unfocus the search box before closing the full UI.
- `Common/UI/UIItemGrid.cs` — made the grid row count height-aware and added configurable empty-state text.
- `Localization/en-US_Mods.SteroidGuide.hjson` — added placeholder, clear label, and search empty-state strings.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide UI with enough available ingredients to populate multiple craftable items.
3. Type a partial item name in the new search field and confirm the grid filters immediately, regardless of case.
4. Change category filters and sort order while a query is active and confirm the filtered results stay sorted correctly.
5. Select an item, then enter a query that removes it from the grid and confirm the recipe tree clears.
6. Clear the query and confirm pagination resets to page 1 and the full grid returns without a fresh inventory-triggered analysis.

## Known Issues
- None.
