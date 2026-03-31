# Spec: Pagination Left Arrow Rendering Fix

## Summary
Fix the analyzer pagination control so the left arrow renders as a clean mirror of the right arrow in every UI state. This removes a visible polish issue in the main item browser without changing pagination behavior.

## Detailed Requirements
1. The left pagination arrow must render without the current broken, jagged, or visibly asymmetric segment artifact seen in game.
2. The left and right pagination arrows must use the same chevron shape, stroke thickness, padding, and visual centering, differing only by horizontal direction.
3. Visual parity must hold across all existing button states: enabled, disabled, and hover.
4. The fix must preserve the current click target, enable/disable rules, page-change behavior, and centered pagination layout.
5. The button must continue using runtime-drawn UI graphics; do not introduce side-specific texture assets for the arrow.
6. Arrow rendering must stay stable at Terraria UI scale and with the current `30x24` button sizing used by the analyzer.
7. The fix must remain fully mod-compatible and must not depend on item IDs, recipe content, or other gameplay data.

## Technical Design
- Modify [Common/UI/UIPaginationArrowButton.cs](/Users/sy/projects/steroid-guide/Common/UI/UIPaginationArrowButton.cs) as the primary implementation point, because it owns the custom chevron drawing inside `UIElement.DrawSelf`.
- Replace the current duplicated left/right point construction with one shared chevron geometry model and mirror it horizontally based on `PaginationArrowDirection`. The design intent is that both buttons come from the same source shape rather than two separately tuned point sets.
- Keep using `SpriteBatch.Draw(...)` with `TextureAssets.MagicPixel` for the background, border, and line segments so the control remains texture-free and consistent with the current UI rendering approach.
- Normalize or pixel-snap the chevron line endpoints before drawing so both arrow strokes land on the same pixel grid. The spec specifically aims to avoid side-specific distortion caused by separate integer truncation or rotation differences in the current line-drawing path.
- Leave [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) responsible for pagination placement and button state management. Only make layout adjustments there if the shared chevron rendering requires a small centering correction.
- No changes are expected in recipe analysis, item scanning, localization, or NPC interaction systems.

## UI/UX
- The pagination row keeps its current placement beneath the item grid.
- Players should perceive the arrows as a matched pair of mirrored chevrons, with no special casing visible on the left side.
- Hover and disabled styling should remain unchanged apart from the left-arrow artifact disappearing.

## Success Criteria
- [ ] In game, the left pagination arrow no longer appears broken or clipped.
- [ ] The left and right arrows appear visually mirrored in normal, hover, and disabled states.
- [ ] Pagination still moves backward/forward correctly and preserves the current button hitboxes and layout.
- [ ] No new textures, hardcoded gameplay data, or unrelated UI regressions are introduced.

## Out of Scope
- Redesigning the pagination row, page label text, or overall analyzer layout.
- Changing filtering, sorting, search, recipe-tree behavior, or analysis logic.
- Reworking the general button color palette or replacing runtime-drawn controls with art assets.
