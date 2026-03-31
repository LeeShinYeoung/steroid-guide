# Spec: Stable Pagination Arrow Graphics

## Summary
Fix the broken-looking previous/next arrow graphics in the craftable item pagination row. Players should see clean, readable navigation arrows instead of distorted line art, while the existing page-change behavior, button states, and overall panel layout remain unchanged.

## Detailed Requirements
1. The previous and next pagination buttons beneath the item grid must render a stable arrow glyph in-game without fragmented pixels, doubled segments, or visibly uneven line joins.
2. Left and right arrows must use the same underlying geometry mirrored across direction. The two buttons must look visually matched rather than hand-tuned as separate shapes.
3. The visible arrow glyph must stay centered inside the existing pagination button bounds and maintain consistent padding from the button border in normal, hover, and disabled states.
4. The fix must preserve the current button states and interactions: hover highlighting, disabled styling on the first/last page, and left-click page changes must keep working exactly as they do now.
5. The rendered arrow should remain readable at Terraria UI scales commonly used by players, including non-default scaled UI where subpixel rotation artifacts are more obvious.
6. The visible arrow shape must not rely on rotated `SpriteBatch.Draw(...)` line segments for its final on-screen geometry. Use a pixel-stable shape that survives Terraria/tModLoader UI scaling cleanly.
7. A simpler arrow style is acceptable if it is more robust. The replacement may be a stepped chevron or compact triangular arrowhead, as long as it clearly communicates previous/next navigation.
8. The change must stay scoped to the item-grid pagination arrows unless a tiny shared helper is necessary. Do not redesign unrelated tree toggles, sort indicators, or other arrow-like controls in this task.
9. The fix must remain compatibility-safe for vanilla and modded content. No hardcoded item IDs, screen-specific offsets, or texture asset dependencies may be introduced.

## Technical Design
- Modify [UIPaginationArrowButton.cs](/Users/sy/projects/steroid-guide/Common/UI/UIPaginationArrowButton.cs) to replace the current rotated-line chevron drawing path with pixel-snapped, axis-aligned `TextureAssets.MagicPixel` rectangle draws inside `UIElement.DrawSelf(...)`.
- Build the arrow glyph from a single shared offset definition and mirror it for `PaginationArrowDirection.Left` versus `PaginationArrowDirection.Right`. This keeps both buttons symmetric and removes duplicated geometry logic.
- Compute the glyph from the button's integer `GetDimensions()` bounds so the final draw positions land on whole pixels. The implementation should derive an inner drawing box, center the glyph within it, and avoid border overlap even when the button size stays `30x24`.
- Keep the existing button frame rendering in [UIPaginationArrowButton.cs](/Users/sy/projects/steroid-guide/Common/UI/UIPaginationArrowButton.cs): background fill, 1px border, and state-driven colors should remain intact unless a tiny spacing tweak is required to center the new glyph.
- Leave pagination state management in [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) unchanged except for minimal alignment adjustments if the new glyph needs slightly different optical centering. `ChangePage(...)`, `UpdatePageText()`, `TryChangePageFromScroll(...)`, and enable/disable behavior are not part of the bug.
- Keep [UIItemGrid.cs](/Users/sy/projects/steroid-guide/Common/UI/UIItemGrid.cs) unchanged unless the Generator needs to verify that scroll-wheel pagination still routes through the same existing page-change flow.
- Do not add external texture files. This should remain a code-rendered UI control compatible with the current tModLoader/XNA UI pipeline.

## UI/UX
- The pagination row should keep the current compact layout: previous arrow, page label, next arrow.
- The arrow glyph may become simpler than the current chevron if that produces a cleaner result at Terraria's pixel scale.
- Disabled arrows should still be visible enough to communicate pagination boundaries, but clearly muted compared with active buttons.

## Success Criteria
- [ ] In-game pagination buttons show intact left/right arrows without broken or overlapping line artifacts.
- [ ] Left and right arrow graphics are visually symmetric and centered within their button frames.
- [ ] Hover, disabled, click, and scroll-wheel pagination behavior remain unchanged after the visual fix.
- [ ] No new texture assets or non-pagination UI redesigns are introduced.

## Out of Scope
- Redesigning the overall pagination row layout or page text styling
- Changing how many items appear per page or how page counts are calculated
- Updating recipe-tree triangles, sort dropdown indicators, or close-button graphics
- Adding texture-based icons or a broader UI art pass
