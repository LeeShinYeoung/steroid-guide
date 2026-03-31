# Spec: Left Filter Sidebar Polish

## Summary
Fix the analyzer's left filter sidebar so category labels always render as player-facing text instead of raw localization keys, and refresh the sidebar chrome so the outer container and per-filter rows feel visually cohesive in game. This is a UI polish change only; filtering behavior stays the same.

## Detailed Requirements
1. Each left sidebar category must display a resolved, human-readable label in game. Raw localization keys such as `Mods.SteroidGuide.UI.Filters.Materials` must never be visible to the player.
2. Filter label resolution must use the mod's localization pipeline first, but if a key is missing, empty, or resolves back to the key string, the UI must show a short English fallback label for that category instead of exposing the key.
3. The existing filter set, order, and click behavior must remain unchanged: `All`, `Weapons`, `Armor`, `Accessories`, `Tools`, `Consumables`, `Placeables`, `Materials`, `Misc`.
4. The entire filter row must remain clickable, and the selected state must still be immediately distinguishable without relying on raw text markers.
5. The left sidebar visual treatment must be adjusted so the outer filter container and the individual filter rows no longer read as mismatched or awkwardly double-bordered elements.
6. The revised styling may soften, reduce, or remove the outer border if needed, but the sidebar must still read as a distinct grouped control area.
7. Unselected, hovered, and selected filter rows must retain clear contrast and state feedback after the visual refresh.
8. The sort control positioned below the filters must continue to align cleanly with the revised sidebar treatment and must not overlap or visually detach from the filter section.
9. The change must not alter recipe analysis results, category classification rules, pagination, search behavior, or recipe-tree interactions.
10. The implementation must remain mod-compatible and must not hardcode gameplay item IDs or mod-specific content assumptions.

## Technical Design
- Modify [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to stop constructing filter labels from raw localization lookups without fallback. Introduce a filter-label resolution path parallel to the existing search placeholder fallback handling so category text is safe even when localization data is missing or malformed.
- Extend the `FilterDefinitions` metadata in [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) so each category carries both its localization key and its fallback display string. Keep `SetFilter`, `UpdateFilterSelectionStates`, and `ApplyFilter` behavior unchanged.
- Update [Common/UI/UIFilterOption.cs](/Users/sy/projects/steroid-guide/Common/UI/UIFilterOption.cs) as the primary rendering surface for row-level polish. This class already owns the indicator, row background, and border drawing in `DrawSelf`, so it should absorb the style refresh rather than pushing presentational logic into the parent UI state.
- Adjust the sidebar container styling in [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) where the filter `UIPanel` is created. The spec expects the container background, padding, and optional border treatment to be retuned so it visually supports the child rows instead of competing with them.
- Preserve the current Terraria UI framework structure: `UIState.OnInitialize` builds the sidebar, `UIElement.OnLeftClick` handles row interaction, and rendering continues through `SpriteBatch` plus `TextureAssets.MagicPixel`-based primitives where custom drawing is needed.
- Keep localization data in [Localization/en-US_Mods.SteroidGuide.hjson](/Users/sy/projects/steroid-guide/Localization/en-US_Mods.SteroidGuide.hjson) authoritative for normal display text. The fallback path is a runtime safety net, not a replacement for valid entries.
- No changes are expected in [Common/ItemCategoryClassifier.cs](/Users/sy/projects/steroid-guide/Common/ItemCategoryClassifier.cs), recipe graph construction, inventory scanning, NPC dialogue, or worldgen systems.

## UI/UX
- The left sidebar should still feel compact and information-dense, but the filter group should read as one composed control instead of a bordered box containing another set of bordered boxes.
- Selected rows should continue using the indicator plus row styling as the primary affordance.
- Hover feedback should remain subtle but obvious enough for mouse-driven use.
- Label text should be stable, readable, and aligned consistently across all filter rows.

## Success Criteria
- [ ] Opening the analyzer never shows raw filter localization keys; each category displays readable text.
- [ ] If a filter localization entry is missing or invalid, the sidebar shows a short fallback label rather than the unresolved key.
- [ ] The left filter container and its buttons appear visually cohesive, with the current awkward border relationship removed.
- [ ] Filter selection, hover feedback, row click targets, and sort/filter functionality continue to work as before.

## Out of Scope
- Reordering filter categories or changing category classification logic.
- Redesigning the main panel, item grid, pagination controls, search box, or recipe tree.
- Adding new filters, new localization locales, or broader theme changes across the whole analyzer UI.
