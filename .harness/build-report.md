# Build Report

## Implemented
- [x] Search box focus now immediately enters text mode on click, keeps keyboard input in the analyzer UI loop, and lets the first `Esc` only release search focus.
- [x] Search placeholder text now resolves through a defensive localization helper with an English fallback, so the UI never shows the raw localization key.
- [x] Search-box drawing now cleanly separates placeholder and typed text, hides the placeholder whenever a query exists, and trims from the right to keep the left-aligned layout visually stable.

## Files Changed
- `Common/UI/UISearchTextBox.cs` — tightened focus activation, preserved mouse-interface blocking while focused, and split placeholder/typed text rendering with stable right-side trimming.
- `Common/UI/RecipeAnalyzerUIState.cs` — resolved search placeholder and clear-label text through fallback-safe localization before constructing the search box.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Launch Terraria with Steroid Guide enabled, open the analyzer UI, click the search field, and type without pressing `Enter`.
3. Confirm results filter immediately, Terraria chat does not open, and interactions behind the analyzer stay blocked while the search field is focused.
4. Verify the placeholder reads as a player-facing hint when empty, disappears as soon as text is entered, and does not overlap or visibly shift when switching between empty and typed states.
5. Click outside the field to remove focus, then press `Esc` once while focused to confirm it only unfocuses the field and a second `Esc` closes the analyzer UI.
6. Confirm pagination resets and any selected recipe tree clears as expected when the active search query filters the selected item out.

## Known Issues
- Manual in-game verification was not run in this environment.
