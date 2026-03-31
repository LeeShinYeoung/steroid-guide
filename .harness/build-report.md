# Build Report

## Implemented
- [x] Replaced recipe-tree crafting-station prose with an icon-first station row that resolves per-tile display items dynamically, falls back to station-name badges, and refreshes with alternative recipe swaps.

## Files Changed
- `Common/UI/UIRecipeTree.cs` — added cached tile-to-item resolution, station-row rendering, hover/fallback handling, and moved station rows directly under the active recipe node.
- `Localization/en-US_Mods.SteroidGuide.hjson` — added the compact localized station label used by the recipe tree.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide UI and select craftable items with recipes that require one or multiple stations.
3. Verify the recipe tree shows a `Station` badge row with item icons where available, fallback name pills when not, and matching station names on hover.
4. Swap to an alternative recipe and confirm the station row updates immediately to the new recipe's required stations.

## Known Issues
- None noted from build verification.
