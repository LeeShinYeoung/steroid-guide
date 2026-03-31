# Build Report

## Implemented
- [x] Fixed station fallback name resolution so modded crafting stations use localized map-entry names instead of internal mod tile identifiers.

## Files Changed
- `Common/UI/UIRecipeTree.cs` — changed tile-name resolution to prefer map-entry/localized names for every station fallback badge and hover label, including modded tiles.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide UI and select a craftable item whose active recipe requires a modded crafting station without a resolved representative icon.
3. Verify the station fallback badge and hover text show the player-facing localized station name rather than the mod tile's internal type name.
4. Swap to an alternative recipe and confirm the displayed station names still refresh to the currently active `UsedRecipe`.

## Known Issues
- None noted from build verification.
