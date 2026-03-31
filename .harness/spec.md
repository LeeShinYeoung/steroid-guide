# Spec: Show Nearby Chest Reference Count

## Summary
Show a clear status line in the recipe analyzer UI telling the player how many nearby chests are currently included in the scan. This makes the result set easier to trust, especially when the player is standing near multiple storage containers or when camera movement changes which chests are considered.

## Detailed Requirements
1. Opening the recipe analyzer UI must show a visible header/status line that reports the number of nearby chests included in the current scan snapshot.
2. The displayed count must come from the existing on-screen chest scan logic, not from a separate proximity heuristic. It should represent the synced chests that the mod actually inspected for crafting analysis.
3. The count must include empty nearby chests if they were part of the current scan, because they were still considered by the scanner even if they contributed no items.
4. In multiplayer, chests skipped as unsynced must not be counted until their contents are available and the scanner actually includes them.
5. The header text must refresh whenever the latest scan changes in a way that affects the displayed chest count, even if the aggregated item dictionary is unchanged.
6. The status line must stay visible and readable at the existing 820x600 layout without overlapping the close button, search box, or other controls.
7. The text must be localization-backed with a readable English fallback and must not expose raw localization keys.
8. The change must remain fully data-driven and mod-compatible. No hardcoded chest ids, tile ids, or world-specific assumptions are allowed.

## Technical Design
- Modify [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) to preserve the latest scan metadata, not just the scanned item totals. The UI state should keep enough state to render both the item analysis and the current nearby-chest count from the same snapshot.
- Update the UI state flow so `OnShow()`, the 30-frame rescan path in `Update(...)`, and the analysis refresh path all consume a shared scan result object or equivalent paired state (`items` + `chestCount`) instead of dropping `ChestCount` after `ItemScanner.ScanAvailableItems(...)`.
- Add a dedicated header/status text element in [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) near the top row of the main panel. It should be updated from the current scan snapshot before or alongside the item grid refresh.
- Change the rescan invalidation logic in [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) so the UI refreshes when either the scanned item dictionary changes or the nearby-chest count changes. This prevents stale copy when chest visibility changes without changing aggregate item totals.
- Reuse the existing [Common/ItemScanner.cs](/Users/sy/projects/steroid-guide/Common/ItemScanner.cs) `ScanResult.ChestCount` as the source of truth. No new chest search algorithm is needed; only the contract between scanner and UI must be carried through consistently.
- Add localization entries in [Localization/en-US_Mods.SteroidGuide.hjson](/Users/sy/projects/steroid-guide/Localization/en-US_Mods.SteroidGuide.hjson) for the chest-count status text. If singular/plural variants are used, both forms must be defined there and resolved in UI code without raw-key leakage.
- No changes are required to [Common/RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs), [Common/RecipeGraphSystem.cs](/Users/sy/projects/steroid-guide/Common/RecipeGraphSystem.cs), NPC dialogue hooks, or worldgen behavior.

## UI/UX
- Place the chest-count line in the panel header area so the player sees it immediately when the analyzer opens.
- Use concise wording such as `Referencing 0 nearby chests` / `Referencing 1 nearby chest` / `Referencing 3 nearby chests`, driven by localization.
- The text should feel like live analysis context, not decorative copy. It should update quietly when the scan refreshes and should not steal focus from search or pagination controls.

## Success Criteria
- [ ] Opening the analyzer with no on-screen chests shows a localized header indicating `0` nearby chests are being referenced.
- [ ] Opening or keeping the analyzer open while two synced on-screen chests are in range shows `2` nearby chests in the header.
- [ ] If chest visibility changes and the nearby-chest count changes without changing aggregate scanned item totals, the header still updates to the new count within the existing rescan cadence.
- [ ] In multiplayer, unsynced nearby chests are not counted until a later scan actually includes them.

## Out of Scope
- Changing which storage sources are scanned beyond the current inventory plus on-screen chest rules
- Redesigning the item grid, recipe tree, filters, search, or pagination
- Adding tooltips, warnings, or per-chest breakdowns
- Optimizing the scanner beyond the UI-state changes needed to keep the chest count accurate
