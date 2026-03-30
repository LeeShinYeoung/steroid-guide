# Spec: Keyword Search for Craftable Items

## Summary
Add a keyword search field to the analyzer UI so players can quickly narrow the craftable item list by name. This reduces scrolling in large modpacks and makes the existing filter, sort, and recipe tree workflow usable when hundreds of craftable results are present.

## Detailed Requirements
1. Add a text input control to the analyzer UI that is visible whenever the main panel is open and clearly indicates it filters the craftable item list.
2. The search must filter only the top-tier craftable item grid; it must not change recipe analysis, chest scanning, or recipe-tree generation.
3. Matching must be case-insensitive and based on the localized display name produced by `Item.SetDefaults(itemId)` and `item.Name`, so vanilla and modded items participate without hardcoded IDs.
4. Search filtering must compose with the existing category filter and sort selection. The result set order must remain the currently selected sort order after keyword filtering is applied.
5. Updating the search query must immediately refresh the visible grid, reset pagination to page 1, and recompute total pages from the filtered result count.
6. If the selected item is filtered out by the current query, the selection must be cleared and the recipe tree must return to its empty state instead of showing stale data.
7. The search box must support normal typing, backspace, delete, left/right arrow caret movement if implemented, `Ctrl+V` paste if the chosen input pattern supports it, and `Escape` or a clear affordance to leave the field without closing the whole UI unintentionally.
8. While the search field is focused, keyboard input must be captured for text entry and must not trigger unrelated game/UI actions beyond the existing analyzer close behavior.
9. Empty results caused by search must display a distinct message that tells the player no craftable items match the current query.
10. The feature must work with large content mods and with the current 30-frame rescan cycle without triggering a new recipe analysis pass for each keystroke; only the already computed UI result set should be refiltered.

## Technical Design
- Modify [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to add search state (`_searchQuery`, focus flag, optional caret/blink state), instantiate the search UI element, and integrate keyword filtering into `ApplyFilter()`.
- Add a dedicated UI text-input element under `Common/UI/` such as `UISearchTextBox.cs` to handle focus, drawing, placeholder text, and text editing. Reusing a standalone element keeps keyboard logic out of the main state class.
- Keep the existing analysis pipeline intact: `RunAnalysisFromScan()` should still produce `AnalysisResult`, and `ApplyFilter()` should become the single place where category filter, keyword filter, sort, selection cleanup, pagination reset, and grid refresh are coordinated.
- Use the localized item name from `new Item().SetDefaults(itemId)` and `item.Name` for matching. Normalize query and item names with a culture-invariant case fold before substring comparison.
- Update [UIItemGrid.cs](/Users/sy/projects/steroid-guide/Common/UI/UIItemGrid.cs) only as needed for the new empty-state copy if the grid itself owns the "no items" message; otherwise keep the message source in `RecipeAnalyzerUIState`.
- Update [RecipeAnalyzerUISystem.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUISystem.cs) only if additional input-routing support is needed while the text box is focused. Prefer keeping close-on-ESC behavior, but avoid double-handling when the search box consumes that keypress.
- Add search-related localization entries in [en-US_Mods.SteroidGuide.hjson](/Users/sy/projects/steroid-guide/Localization/en-US_Mods.SteroidGuide.hjson) for placeholder text, clear label if present, and the search-empty-state message.
- Use tModLoader/Terraria input APIs appropriate for UI text entry, specifically `Main.GetInputText(...)` for character capture and `PlayerInput.WritingText`/equivalent UI focus signaling to prevent gameplay controls from consuming typed input.

## UI/UX
- Place the search field in the right pane above the item grid so it scopes naturally to the craftable-items list rather than the left filter sidebar.
- Placeholder copy should be explicit, for example "Search craftable items...".
- Show the current query immediately in the field and provide a visible focused state so players understand keyboard input is captured.
- Preserve the current category filter buttons, sort dropdown, pagination row, and recipe tree layout. The search control should fit within the existing 820x600 panel without shrinking the recipe tree below its current practical size.
- When no matches remain, keep the rest of the UI interactive and show an inline empty state instead of a modal or chat message.

## Success Criteria
Measurable criteria the Evaluator will check:
- [ ] Typing a partial item name filters the craftable item grid to matching items only, regardless of letter case.
- [ ] Category filter and sort still work when a search query is active, and the visible ordering matches the selected sort option.
- [ ] Changing or clearing the query resets pagination correctly and never leaves the UI on an invalid page.
- [ ] If a previously selected item no longer matches the query, the recipe tree clears instead of showing the old selection.
- [ ] Search interactions do not trigger a full recipe re-analysis on every keystroke and remain responsive with large modded recipe sets.

## Out of Scope
- Searching recipe-tree ingredient nodes or crafting-station text.
- Adding fuzzy matching, typo tolerance, tags, or advanced search operators.
- Persisting the last query across UI close/reopen or across sessions.
- Reworking the existing filter button visuals, sort dropdown style, or broader localization coverage outside search-related strings.
