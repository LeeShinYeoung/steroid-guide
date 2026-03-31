# Build Report

## Implemented
- [x] Removed the recipe-tree `Station` label so crafting-station rows render as icon badges or fallback chips only.

## Files Changed
- `Common/UI/UIRecipeTree.cs` — removed the dedicated station label plumbing and updated station badge layout/wrapping to start from the first badge while preserving hover behavior and ancestor-line alignment.
- `Localization/en-US_Mods.SteroidGuide.hjson` — removed the unused `UI.RecipeTree.StationLabel` localization entry.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide recipe tree for an item whose selected recipe requires one or more crafting stations.
3. Verify the station row shows only station icon badges or fallback chips, with no `Station` text pill, for the root recipe and any expanded child recipe nodes.
4. Hover station badges and fallback chips to confirm the existing tooltip/name behavior still works, and verify multi-station rows still wrap cleanly without misaligned tree connectors.

## Known Issues
- None.
