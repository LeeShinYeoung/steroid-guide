# Build Report

## Implemented
- [x] Added a localized nearby-chest status line to the recipe analyzer UI that reflects the latest on-screen chest scan snapshot.
- [x] Preserved scan metadata in UI state so chest-count changes refresh the header even when aggregated item totals do not change.

## Files Changed
- `Common/UI/RecipeAnalyzerUIState.cs` — stored the latest `ScanResult`, updated scan invalidation logic, and rendered the localized chest-count header.
- `Localization/en-US_Mods.SteroidGuide.hjson` — added English strings for singular and plural nearby-chest status text.

## How to Test
1. Build with tModLoader.
2. Open the Steroid Guide UI with no nearby synced chests and confirm the header reads `Referencing 0 nearby chests`.
3. Move near one synced chest, then multiple synced chests, and confirm the header updates to the correct singular/plural count.
4. Keep the UI open while moving the camera so chest visibility changes without item totals changing, and confirm the header still refreshes within the normal rescan cadence.
5. In multiplayer, verify unopened unsynced nearby chests are not counted until the scanner actually includes them.

## Known Issues
- None observed.
