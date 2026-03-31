# Spec: Direct Search Input Focus

## Summary
Fix the search box so players can type into it immediately after clicking, without opening Terraria's chat first. At the same time, remove the right-side `Clear` affordance so the control behaves like a compact single-purpose filter field and leaves text entry/clearing entirely to normal keyboard input.

## Detailed Requirements
1. Clicking anywhere inside the search box must give it active text focus on the first click. Players must not need to press `Enter`, open the chat UI, or click twice before typing.
2. While the search box is focused, keyboard input must flow into the search query even when vanilla chat is closed. This includes normal character input, backspace-driven deletion, and IME/text-composition paths supported by Terraria's text input APIs.
3. Search focus must belong only to the custom Steroid Guide UI. Focusing the search box must not open the chat window, toggle inventory/chat state, or require any vanilla text-entry overlay.
4. The existing filter behavior must stay intact: each text change immediately reapplies the UI-level filter to `AnalysisResult.TopTierItems`, resets pagination to page 1, and clears the selected recipe tree if the selected item is filtered out.
5. Pressing `ESC` while the search box is focused must release only the search focus on that first keypress. The same `ESC` keypress must not also close the Steroid Guide panel.
6. Clicking outside the search box while it is focused must release focus cleanly without leaving text-entry state stuck on for later frames.
7. Remove the search box's right-side `Clear` button entirely. No text label, click target, reserved width, or localization dependency for `Clear` should remain in the rendered control.
8. After removing the clear button, typed text and placeholder text must use the full input width without shifting, clipping against phantom padding, or overlapping the caret.
9. Reopening the UI must continue to reset the search query to empty unless the current UI flow already intentionally persists it elsewhere. This issue does not add persistence across UI sessions.
10. The fix must remain compatibility-safe for vanilla and modded items. No item-specific exceptions, hardcoded IDs, or search-result special cases are allowed.

## Technical Design
- Modify [UISearchTextBox.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/UI/UISearchTextBox.cs) to become a true focused text-input control instead of a text box with an embedded clear action.
- Remove `_clearText`, `HasClearButton`, `GetClearButtonRectangle()`, and all related draw/click logic from `UISearchTextBox`. The constructor should only need placeholder text plus any max-length configuration.
- Keep focus ownership inside the custom UI control, but make the text-entry path explicit and frame-stable:
  - use `UIElement.LeftClick(...)` to acquire focus
  - use `UIElement.Update(...)` or a small focus-aware helper invoked from the UI update path to drive text capture every frame
  - continue using Terraria/tModLoader text APIs already compatible with the project: `Main.GetInputText(...)`, `PlayerInput.WritingText`, `Main.clrInput()`, and `Main.LocalPlayer.mouseInterface`
- If the current `UISearchTextBox.Update(...)` timing is why text only arrives when chat is open, move the "focused text input is active this frame" responsibility to [RecipeAnalyzerUISystem.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/UI/RecipeAnalyzerUISystem.cs) or another single per-frame owner so the search field can request text input regardless of vanilla chat state. The important contract is that the Generator must solve the first-click typing bug by owning text focus inside the Steroid Guide UI rather than depending on vanilla chat.
- Keep `ESC` handling split across layers:
  - [RecipeAnalyzerUISystem.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/UI/RecipeAnalyzerUISystem.cs) should continue intercepting `Keys.Escape` at the panel level
  - [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/UI/RecipeAnalyzerUIState.cs) should continue routing that first `ESC` press to the search control through `HandleEscapeKey()`
  - `UISearchTextBox` should only report whether it consumed the escape press by unfocusing itself
- Update [RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/UI/RecipeAnalyzerUIState.cs) to instantiate the search box without a clear-button label and to preserve the existing `OnTextChanged`, `Reset()`, and immediate `ApplyFilter()` flow.
- Update [Localization/en-US_Mods.SteroidGuide.hjson](/Users/sy/projects/steroid-guide-planner-searchbar/Localization/en-US_Mods.SteroidGuide.hjson) to remove the now-unused `UI.SearchClear` string if it is no longer referenced anywhere.
- No changes are expected in [RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/RecipeAnalyzer.cs), [ItemScanner.cs](/Users/sy/projects/steroid-guide-planner-searchbar/Common/ItemScanner.cs), recipe graph construction, or NPC behavior.

## UI/UX
- The search box should behave like Recipe Browser-style direct text entry: click the field and type immediately.
- The control should read visually as a single uninterrupted input field, not an input plus trailing action chip.
- Clearing text is keyboard-driven only for this scope. Backspace/delete and full reset on reopen are sufficient; no replacement icon or inline button should be introduced.
- Existing placeholder styling, panel alignment, and search-result empty state messaging should remain consistent with the current UI unless small spacing adjustments are required after removing the clear area.

## Success Criteria
- [ ] Clicking the search field and typing immediately filters results without opening Terraria chat.
- [ ] The first typed character is captured reliably even when chat was previously closed.
- [ ] `ESC` unfocuses the search field first and does not close the full UI on the same keypress.
- [ ] Clicking outside the field exits search focus cleanly and does not leave text-entry mode stuck on.
- [ ] The search field renders without any right-side `Clear` text/button and uses the reclaimed width correctly.

## Out of Scope
- Persisting search text across UI closes or world reloads
- Adding advanced search syntax, tokenization, or fuzzy matching
- Redesigning filters, sorting, pagination, or recipe-tree behavior
- Adding a replacement icon button for clearing text
