# Build Report

## Implemented
- [x] Made the search box capture text immediately on first click without opening Terraria chat by moving focused text input handling to a dedicated per-frame UI owner.
- [x] Removed the search box's right-side `Clear` affordance and reclaimed the full input width while keeping existing filter, reset, and ESC-unfocus behavior.

## Files Changed
- `Common/UI/UISearchTextBox.cs` — removed the embedded clear-button code, kept focus ownership inside the control, and exposed focused text capture as an explicit per-frame step.
- `Common/UI/RecipeAnalyzerUISystem.cs` — runs the search box's focused text-input update after UI click processing so first-click typing works with chat closed.
- `Common/UI/RecipeAnalyzerUIState.cs` — instantiates the simplified search box and forwards the per-frame search input update.
- `Localization/en-US_Mods.SteroidGuide.hjson` — removed the unused `UI.SearchClear` localization entry.

## How to Test
1. Build with tModLoader.
2. Open the Steroid Guide analyzer UI in-game and click inside the search field once while vanilla chat is closed.
3. Type immediately and verify the first typed character appears in the field and filters the item grid right away.
4. Press `ESC` once while the search field is focused and verify the field loses focus without closing the analyzer UI.
5. Focus the search field again, click outside it, and verify typing no longer edits the query until the field is clicked again.
6. Verify the search box no longer renders any right-side `Clear` text/button and that placeholder/typed text use the full field width.

## Known Issues
- None.
