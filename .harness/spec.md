# Spec: Graphical Category Filter Selector

## Summary
Replace the analyzer's left sidebar category controls with a graphical single-select indicator instead of text markers like `[ ]` and `[*]`. This keeps the filter behavior unchanged while making the UI easier to scan and more consistent with the rest of the analyzer panel.

## Detailed Requirements
1. The left filter sidebar must keep the existing category set and order: `All`, `Weapons`, `Armor`, `Accessories`, `Potions`, `Tools`, `Misc`.
2. Filter selection must remain single-select, with `All` selected by default when the UI opens.
3. Each category row must show a graphical selection indicator, not literal text prefixes such as `[ ]` or `[*]`.
4. The selected category must be visually distinct at a glance through the indicator itself and row styling, without relying on the label text changing.
5. Clicking anywhere on a category row, including the indicator and label, must apply that filter immediately.
6. Existing filter behavior must remain intact: the current filter still drives `ApplyFilter()`, still combines with search and sort, and still resets paging the same way it does today.
7. The filter sidebar must continue to fit inside the current analyzer layout at the existing panel size and under Terraria UI scale changes.
8. The control must be rendered without item-specific hardcoding or content-mod assumptions, and it must not require new sprite assets.
9. This change is scoped to the left category filter UI. Sort dropdown behavior, recipe analysis, and item categorization rules must not change unless a minimal compatibility adjustment is required for the new control.

## Technical Design
- Modify [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to stop storing category controls as plain `UIText` instances whose labels are rebuilt with `[ ]` and `[*]`.
- Introduce a dedicated sidebar option UI element in `Common/UI/` such as `UIFilterOption` or an equivalently named reusable control. It should own row hit testing, hover state, selected state, and drawing of the graphical indicator plus label.
- Keep `FilterCategory`, `SetFilter(...)`, and `ApplyFilter()` as the behavioral source of truth. The refactor should change presentation ownership, not filtering logic.
- Use the existing tModLoader UI stack (`UIElement`, `UIText` or direct text drawing, and `DrawSelf(SpriteBatch)`) so the new control integrates with the current `UIState` layout and input model.
- Render the selector with built-in drawing primitives already used elsewhere in the mod, such as `TextureAssets.MagicPixel`, to avoid introducing external textures.
- Store the sidebar controls in a dictionary keyed by `FilterCategory`, but update them through explicit selected-state APIs like `SetSelected(bool)` instead of string rebuilding.
- Preserve the existing sidebar panel footprint in `OnInitialize()` so the search box, item grid, sort control, and recipe tree keep their current positions.
- Do not change item classification rules in `GetItemCategory(int itemId)`, the inventory scan cadence, or any recipe graph/analyzer data structures.

## UI/UX
- Each filter row should read as a compact radio-style option: indicator on the left, label aligned beside it.
- Hover feedback should make the entire row feel clickable, not just the text glyphs.
- Selected and unselected states should remain legible against the current panel colors without overwhelming the rest of the analyzer UI.
- The sidebar should look stable and aligned even with longer labels like `Accessories`.

## Success Criteria
- [ ] The category filter sidebar no longer displays literal `[ ]` or `[*]` markers.
- [ ] The currently selected category is communicated through a graphical indicator and consistent row styling.
- [ ] Clicking a category still updates the visible top-tier item list using the existing filter logic.
- [ ] Search, sort, pagination reset, and selected-item clearing behavior remain unchanged after switching filters.
- [ ] No new texture assets or item-ID-specific logic are added for this feature.

## Out of Scope
- Redesigning the sort dropdown or converting its text markers in the same task.
- Changing category definitions, adding new categories, or altering `GetItemCategory(...)`.
- Reworking analyzer layout, pagination, recipe tree rendering, or NPC interactions.
