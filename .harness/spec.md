# Spec: Sort Control Visual Refresh

## Summary
Refresh the analyzer's sort UI so it matches the rest of the sidebar instead of looking like placeholder text UI. The player should see a compact sort icon in the trigger, no trailing arrow button on the right edge, and graphical selected-state markers inside the dropdown instead of literal `[ ]` / `[*]` text.

## Detailed Requirements
1. The collapsed sort trigger must no longer render the literal `"Sort: "` prefix. It must show a graphical sort icon plus the currently selected sort label.
2. The sort trigger must no longer include a right-side arrow affordance or arrow-only button region. Opening and closing the dropdown must still work by clicking the trigger body.
3. The currently selected sort mode must remain visible in the collapsed trigger using the existing four labels: `Rarity`, `Name`, `Value`, and `Recipe Depth`.
4. Opening the sort dropdown must still present the same four sort modes in the same order and must continue to call the existing sorting pipeline when a mode is selected.
5. Dropdown rows must replace the text-based selection markers (`[ ]`, `[*]`) with graphical indicators. The selected/unselected treatment must visually match the left filter sidebar's indicator language closely enough that both controls read as part of one UI family.
6. Each sort row must keep a full-row click target, clear hover feedback, and an immediately recognizable selected state without relying on text prefixes.
7. The dropdown must still close immediately after selecting a sort option, and the item grid must refresh using the newly selected sort.
8. The change must not alter sort semantics:
   `Rarity` remains rarity descending then item ID ascending;
   `Name` remains alphabetical ascending;
   `Value` remains sell value descending then item ID ascending;
   `Recipe Depth` remains recipe depth descending then rarity descending.
9. The visual refresh must stay scoped to the sort trigger/dropdown. It must not change filter behavior, search behavior, pagination behavior, recipe-tree interactions, or recipe analysis results.
10. The implementation must remain mod-compatible and must not hardcode item IDs, mod content, or assumptions about vanilla-only assets.

## Technical Design
- Modify [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to stop building the sort UI from a `UITextPanel<string>` plus plain `UIText` rows. This file should continue owning sort state (`_currentSort`, `_sortDropdownOpen`) and the existing `ToggleSortDropdown`, `SelectSort`, and `ApplyFilter` flow.
- Replace the current trigger construction in `OnInitialize()` with a custom sort-trigger element that can draw:
  a sort icon,
  the active sort label,
  hover/open visual state,
  and no trailing arrow glyph.
- Add a dedicated dropdown-row renderer for sort options, likely a new file such as [UISortOption.cs](/Users/sy/projects/steroid-guide/Common/UI/UISortOption.cs), rather than continuing to use raw `UIText`. This keeps the graphical indicator, hover styling, and selected-state rendering encapsulated in one place.
- Add a dedicated trigger renderer, likely [UISortButton.cs](/Users/sy/projects/steroid-guide/Common/UI/UISortButton.cs), if `UITextPanel<string>` cannot express the required icon-first layout cleanly. The trigger should be a single clickable element whose `OnLeftClick` still delegates back to `RecipeAnalyzerUIState.ToggleSortDropdown()`.
- Reuse the existing sidebar visual language from [UIFilterOption.cs](/Users/sy/projects/steroid-guide/Common/UI/UIFilterOption.cs) as the reference for the dropdown indicator shape, border treatment, and selected-state contrast. Exact pixel-for-pixel reuse is not required, but the design should feel intentionally related.
- Keep the dropdown container in [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) positioned under the filter group as it is today. Only its child row rendering and trigger styling should change.
- Preserve the current Terraria UI hooks and rendering model:
  `UIState.OnInitialize()` for construction,
  `UIElement.OnLeftClick` for interaction,
  and `SpriteBatch` plus `TextureAssets.MagicPixel` for custom shapes.
- If sort labels need centralized display text, add a small mapping helper in [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) so both the trigger and dropdown rows use the same label source. This is a display concern only; the `SortCriteria` enum and comparison logic stay unchanged.
- No changes are expected in [RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs), [ItemCategoryClassifier.cs](/Users/sy/projects/steroid-guide/Common/ItemCategoryClassifier.cs), [ItemScanner.cs](/Users/sy/projects/steroid-guide/Common/ItemScanner.cs), NPC code, or world generation.

## UI/UX
- The sort trigger should read as a compact sidebar control, not a sentence fragment.
- The sort icon should communicate purpose at a glance without overpowering the current sort label.
- Dropdown rows should mirror the left filter selector's visual logic so the sidebar feels coherent.
- Hovered, selected, and idle states must remain distinguishable against the current dark sidebar palette.

## Success Criteria
- [ ] The collapsed sort control shows an icon plus the current sort label and never displays the literal `"Sort: "` text.
- [ ] The sort trigger no longer shows a right-edge arrow affordance, but clicking it still opens and closes the dropdown reliably.
- [ ] Sort dropdown rows no longer render `[ ]` or `[*]`; selected state is shown with graphics consistent with the left filter sidebar.
- [ ] Selecting any sort option still closes the dropdown and reorders the current result set using the existing sort rules.

## Out of Scope
- Adding new sort modes or changing the order of the existing sort modes.
- Localizing sort labels or redesigning unrelated sidebar controls beyond what is needed for visual consistency.
- Changing filter classification, search input behavior, pagination arrows, recipe-tree rendering, or analysis algorithms.
