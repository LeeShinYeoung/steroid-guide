# Spec: Search Box Focus and Placeholder Fixes

## Summary
Fix the analyzer search box so players can click into it and type immediately without opening Terraria chat. The placeholder must render as a proper localized hint, disappear as soon as the user enters text, and stop visually shifting when the field transitions from empty to typed content.

## Detailed Requirements
1. Clicking the analyzer search box must place it into an active text-entry state immediately, without requiring `Enter`, chat open state, or any other vanilla text UI to be opened first.
2. While the search box is focused, keyboard input must continue flowing into the box through the analyzer UI update loop, and world interactions behind the panel must remain blocked as they are today.
3. Clicking outside the search box must remove search focus without closing the analyzer UI.
4. Pressing `Esc` while the search box is focused must only clear search focus on that keypress; the analyzer UI close behavior should remain on the next `Esc`, matching the current `RecipeAnalyzerUISystem` flow.
5. The placeholder text must display as a resolved user-facing localized string, never the raw key `Mods.SteroidGuide.UI.SearchPlaceholder`.
6. If localization lookup fails for any reason, the UI must fall back to a short built-in English hint rather than exposing a raw localization key.
7. The placeholder must render only when the effective query is empty. Once any query text is present, only the typed value and optional caret/clear affordance may be drawn.
8. The text layout must stay left-aligned and visually stable when moving between placeholder and typed text. Typing into the box must not make the placeholder appear to slide, jump to the right, or overlap the entered query.
9. Existing search result behavior must stay intact: filtering still targets top-tier craftable items only, remains case-insensitive, resets pagination to page 1 on query change, and clears the selected recipe tree if the selected item falls out of the filtered result set.
10. The fix must remain content-mod compatible and must not hardcode item IDs, chat state assumptions, or mod-specific logic.

## Technical Design
- Modify `Common/UI/UISearchTextBox.cs` to make mouse focus the authoritative trigger for text-entry mode and keep all placeholder/caret/clear-button drawing decisions inside that control.
- Update `Common/UI/RecipeAnalyzerUIState.cs` to keep using the search box as the single source of truth for query changes, but resolve placeholder text defensively so the UI never surfaces a raw localization key.
- Keep `Common/UI/RecipeAnalyzerUISystem.cs` responsible for the analyzer-wide `Esc` close flow; the search box should consume the first `Esc` through `HandleEscape()` and let the system close the panel only after focus has already been released.
- Update `Localization/en-US_Mods.SteroidGuide.hjson` only if the existing copy needs a clearer player-facing hint or if a missing/fallback-safe string needs to be added.
- Continue using Terraria/tModLoader text-input APIs already present in the mod: `UIElement.LeftClick`, `UIElement.Update`, `UIElement.ContainsPoint`, `Main.GetInputText(...)`, `PlayerInput.WritingText`, `Main.clrInput()`, and `Main.LocalPlayer.mouseInterface`.
- Separate placeholder rendering from typed-text rendering in the search box draw path so trimming logic applies to the active query, not to placeholder state. If width trimming is needed, it should preserve a stable left-aligned anchor and never draw placeholder text once `_text` is non-empty.
- Do not change `RecipeAnalyzer`, `RecipeGraphSystem`, or the search filtering algorithm unless a minimal interface contract change is required between the text box and `RecipeAnalyzerUIState`.

## UI/UX
- The search field keeps its current placement and general styling.
- Empty state: a localized hint such as "Search craftable items..." appears in a subdued color.
- Focused state: the field clearly accepts typing immediately after click, with the existing caret/clear affordances preserved or cleaned up as needed.
- Filled state: only the entered query is visible; the placeholder is fully hidden and does not animate or drift.

## Success Criteria
- [ ] In game, clicking the search box and typing filters results immediately without opening chat via `Enter`.
- [ ] The placeholder renders as a readable hint string instead of `Mods.SteroidGuide.UI.SearchPlaceholder`.
- [ ] After typing at least one character, the placeholder is no longer visible and no right-shift/jumping artifact appears in the field.
- [ ] Clicking outside the field removes focus, and pressing `Esc` while focused only unfocuses the field on the first keypress.
- [ ] Existing search filtering, pagination reset, and recipe-tree deselection behavior still work after the fix.

## Out of Scope
- Reworking the analyzer layout, category filters, sorting UI, or recipe tree presentation.
- Changing which items are considered craftable or searchable.
- Adding multi-field search, fuzzy matching, or localization files beyond what is required for the existing search hint.
