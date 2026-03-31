# Build Report

## Implemented
- [x] Added hover-scoped mouse-wheel pagination for the craftable item grid so each wheel notch moves exactly one page through results.
- [x] Routed wheel pagination through the existing page-change logic so grid contents, `Page X/Y` text, and pagination button enabled states stay in sync.
- [x] Kept wheel pagination isolated to `UIItemGrid`, leaving recipe-tree scrolling and other analyzer controls unchanged.

## Files Changed
- `Common/UI/RecipeAnalyzerUIState.cs` — hooked the item grid wheel callback into a guarded pagination entry point that validates page bounds before calling the existing `ChangePage(int delta)` method.
- `Common/UI/UIItemGrid.cs` — added a hover-only scroll-wheel handler and callback so the grid reports wheel pagination requests without owning page state.
- `.harness/build-report.md` — updated the report for this feature.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide analyzer UI with enough craftable results to produce multiple pages.
3. Hover the cursor over the craftable item grid and scroll up/down; confirm each wheel notch moves exactly one page and updates the page label plus arrow enabled state.
4. Hover the pagination row, search box, filter sidebar, sort dropdown, recipe tree, and empty panel space; confirm wheel input does not change the current grid page there.
5. Hover the recipe tree and scroll; confirm the tree still scrolls normally instead of paging the item grid.
6. On the first and last page, keep scrolling past the bounds; confirm nothing desyncs and the page stays within valid limits.

## Known Issues
- None.
