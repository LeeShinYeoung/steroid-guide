# Build Report

## Implemented
- [x] Added safe filter label resolution so the analyzer sidebar never shows raw localization keys and falls back to short English labels when localization is missing or malformed.
- [x] Polished the left filter sidebar and adjacent sort control so the group reads as one cohesive control area while preserving filter order, click behavior, hover feedback, and selected-state clarity.

## Files Changed
- `Common/UI/RecipeAnalyzerUIState.cs` — extended filter metadata with fallback labels, routed filter text through the localization fallback helper, and refreshed the sidebar/sort container styling.
- `Common/UI/UIFilterOption.cs` — retuned row rendering with softer borders, clearer hover/selected states, and more cohesive accent/separator treatment.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide analyzer UI and inspect the left filter sidebar.
3. Verify all categories read `All`, `Weapons`, `Armor`, `Accessories`, `Tools`, `Consumables`, `Placeables`, `Materials`, and `Misc` rather than raw localization keys.
4. Hover and click different filter rows to confirm the full row remains clickable, the active row is clearly selected, and the sort control still aligns cleanly below the filters.

## Known Issues
- None.
