# Spec: Graphic Close Button for Recipe Analyzer UI

## Summary
Replace the recipe analyzer panel's top-right text `X` close button with a graphic close icon that matches the rest of the custom UI. This removes the placeholder-looking text control and ensures the close affordance reads cleanly at a glance, with the icon centered correctly inside its clickable bounds.

## Detailed Requirements
1. The recipe analyzer UI must use a graphic close button in the panel's top-right corner instead of a `UITextPanel<string>` that renders the literal character `X`.
2. The visible `X` icon must be centered horizontally and vertically within the button's rendered bounds, including at normal Terraria UI scale and when the mod UI is drawn with the game's UI scaling applied.
3. The button must keep the current close behavior: left click closes the analyzer through the existing `RecipeAnalyzerUISystem.HideUI()` flow, without changing ESC close handling or NPC distance-based auto-close.
4. The button's hitbox must remain easy to target and must stay anchored to the top-right edge of the existing main panel, without overlapping the nearby-chest status text.
5. The button must use the same visual language as the rest of the analyzer UI: custom-drawn geometry, clear hover feedback, and no fallback to raw text markers for the control itself.
6. The icon rendering must be stable and symmetric. Stroke placement should avoid the current problem where text glyph metrics make the close mark feel off-center relative to the button rectangle.
7. The change must stay scoped to the analyzer close control and any minimal layout adjustments needed to fit it cleanly. Search, filters, sort, pagination, recipe analysis, and recipe tree behavior are not part of this feature.

## Technical Design
- Modify [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to replace the current inline `UITextPanel<string>("X")` construction with a dedicated close-button UI element, while preserving the existing top-right anchoring and `HideUI()` click action.
- Create [Common/UI/UICloseButton.cs](/Users/sy/projects/steroid-guide/Common/UI/UICloseButton.cs) as a custom `UIElement` responsible for drawing the button background, border, and centered `X` icon. Reuse the project's existing UI drawing approach with `SpriteBatch` and `TextureAssets.MagicPixel` rather than introducing a font glyph dependency.
- Render the `X` icon as two diagonal strokes derived from shared geometry constants, with pixel-snapped endpoints so the icon stays visually centered and does not blur or drift off-axis.
- Use `IsMouseHovering` in `DrawSelf` to provide hover-state color changes consistent with `UISortButton`, `UIFilterOption`, and `UIPaginationArrowButton`.
- Keep the close interaction in the current UI layer. No changes are needed in [Common/UI/RecipeAnalyzerUISystem.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUISystem.cs) beyond continuing to call `HideUI()` through the existing button click hook.
- No item IDs, NPC IDs, or mod-specific content assumptions are needed for this work. The feature is a presentation-layer change inside the analyzer UI only.

## UI/UX
- The close control should read as a button first and an icon second: visible button chrome, a centered graphic `X`, and a stronger hover state when the cursor is over it.
- The icon should be optically balanced inside the square button, not biased upward, downward, left, or right by text padding or font ascent.
- The control should continue to sit flush with the panel's top-right affordance area so players can close the UI instinctively.

## Success Criteria
- [ ] Opening the analyzer shows a graphic close button in the top-right corner instead of a text-rendered `X`.
- [ ] The `X` icon appears centered inside the button and does not look offset at runtime.
- [ ] Hovering the button gives clear visual feedback, and clicking it closes the analyzer exactly as before.
- [ ] ESC close, distance-based auto-close, and the nearby-chest header remain unchanged.

## Out of Scope
- Redesigning the full analyzer panel theme
- Changing pagination, sort, search, filters, or recipe tree interactions
- Adding new textures, localization keys, or unrelated UI polish beyond what is required for the close button
