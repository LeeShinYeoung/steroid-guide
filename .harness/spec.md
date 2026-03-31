# Spec: Hover Scroll Pagination for Craftable Item Grid

## Summary
Allow players to change pages with the mouse wheel while the cursor is inside the craftable item grid. This removes unnecessary travel to the pagination arrows while keeping the behavior tightly scoped so recipe-tree scrolling and normal world scrolling are unaffected outside the grid area.

## Detailed Requirements
1. When the Recipe Analyzer UI is open and the mouse cursor is over the craftable item list area, mouse-wheel input must move the item grid pagination by one page per wheel notch.
2. Scrolling upward must move toward the previous page and scrolling downward must move toward the next page, matching common list-navigation expectations.
3. The behavior must only trigger when the cursor is inside the interactive bounds of `UIItemGrid`. Hovering the pagination row, search box, filter sidebar, sort dropdown, recipe tree, or any empty space in the panel must not change pages.
4. If the current result set has only one page, or the player is already at the first/last page for the requested direction, wheel input must do nothing and must not desync page text, selection state, or button enabled state.
5. Wheel-based pagination must reuse the same page-change path as the arrow buttons so `UpdateGrid()`, `UpdatePageText()`, and pagination button state remain consistent.
6. The feature must not interfere with existing recipe-tree scrolling. When the cursor is over the recipe tree, its `UIList`/`UIScrollbar` wheel behavior must continue to work normally.
7. No new visible controls or localization strings are required. This is an input-behavior improvement only.

## Technical Design
- Modify [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to own a wheel-pagination entry point that validates page bounds before delegating to the existing `ChangePage(int delta)` logic.
- Modify [UIItemGrid.cs](/Users/sy/projects/steroid-guide/Common/UI/UIItemGrid.cs) to handle scroll-wheel input using the tModLoader `UIElement.ScrollWheel(UIScrollWheelEvent evt)` override or `OnScrollWheel` event surface, because the grid already defines the exact hover rectangle via `GetDimensions()`/`ContainsPoint(Main.MouseScreen)`.
- Add a small event or callback from `UIItemGrid` back to `RecipeAnalyzerUIState` so the grid detects hover + wheel input, while the UI state remains the single source of truth for `_currentPage`, `_totalPages`, `_filteredItems`, and selected-item preservation.
- Keep the implementation within the existing UI layer. No changes are needed in `RecipeAnalyzerUISystem`, `RecipeAnalyzer`, `ItemScanner`, or recipe graph code because pagination is a presentation concern.
- Use existing tModLoader UI APIs:
  - `UIElement.ContainsPoint(Vector2 point)` for hover scoping.
  - `UIElement.ScrollWheel(UIScrollWheelEvent evt)` or `OnScrollWheel` for wheel capture inside the hovered element.
  - Existing `UIList.ScrollWheel(UIScrollWheelEvent evt)` behavior in [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) should remain untouched so tree scrolling continues to work.
- Keep the page step at exactly one page per wheel event. Do not add acceleration, inertial scrolling, or partial-row scrolling.

## UI/UX
- No layout changes.
- The item grid gains a hover-only affordance through behavior: wheel input over the grid pages the results immediately.
- The pagination arrows remain visible and functional as the explicit alternative input path.
- Because the trigger area is limited to the grid rectangle, accidental wheel pagination while reading the recipe tree or moving across other controls must not occur.

## Success Criteria
- [ ] With two or more item pages available, wheel input over the craftable item grid changes exactly one page per notch and updates the `Page X/Y` label and arrow enabled states correctly.
- [ ] Wheel input outside the craftable item grid does not change the current item page.
- [ ] Wheel input over the recipe tree still scrolls the tree instead of paging the item grid.
- [ ] Reaching the first or last page does not throw errors and does not move beyond valid bounds.

## Out of Scope
- Changing pagination layout, arrow visuals, or page label styling.
- Adding drag, touch, or keyboard pagination shortcuts.
- Introducing smooth scrolling inside the item grid.
- Any recipe-analysis or data-refresh changes unrelated to page navigation.
