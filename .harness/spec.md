# Spec: First-Open Analyzer UI Exception

## Summary
Fix the client-side `Index was outside the bounds of the array` error that appears in chat the first time the player opens the Steroid Guide's `Analyze Recipes` UI. The analyzer must open cleanly on the first attempt, keep rendering valid craftable items, and remain compatible with large modded recipe sets.

## Detailed Requirements
1. Opening the analyzer from the Steroid Guide NPC must not print an exception message to chat on the first open after entering a world, nor on later opens in the same session.
2. The fix must cover the full first-open path triggered by `SteroidGuideNPC.OnChatButtonClicked(...)`, `RecipeAnalyzerUISystem.ShowUI(...)`, `RecipeAnalyzerUIState.OnShow()`, and the first UI draw/update cycle.
3. UI rendering must tolerate any craftable item ID returned by the existing recipe analysis, including modded items, without assuming that every item-backed asset array can be indexed blindly during the first frame.
4. If an item icon or animation frame cannot be resolved safely for a frame, the analyzer must fail soft by keeping the row interactive and text-visible instead of throwing or surfacing a chat error.
5. The protection must apply anywhere the analyzer renders item visuals from analysis data, including the top-tier item grid and recipe tree rows.
6. Search, sorting, pagination, selection, recipe-tree expansion, and alternative recipe switching must continue to behave as they do today once the exception is removed.
7. The fix must not hardcode vanilla or mod item IDs, and it must not special-case specific content mods.
8. The analyzer's craftability results and recipe graph logic are not to be changed unless a minimal guard is required to keep invalid visual state out of the UI layer.

## Technical Design
- Inspect and adjust the first-open flow in [Common/UI/RecipeAnalyzerUISystem.cs](/Users/sy/projects/steroid-guide-pge10/Common/UI/RecipeAnalyzerUISystem.cs), [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide-pge10/Common/UI/RecipeAnalyzerUIState.cs), [Common/UI/UIItemGrid.cs](/Users/sy/projects/steroid-guide-pge10/Common/UI/UIItemGrid.cs), and [Common/UI/UIRecipeTree.cs](/Users/sy/projects/steroid-guide-pge10/Common/UI/UIRecipeTree.cs).
- Add a small shared helper in `Common/UI/` if needed so item-icon rendering uses one guarded code path instead of duplicating fragile array access in both the grid and the tree.
- Keep `RecipeAnalyzer`, `RecipeGraphSystem`, and `ItemScanner` as the source of recipe data unless investigation proves the first-open exception comes from handing the UI an invalid item ID. The default assumption should be that the bug is in UI initialization or rendering, not in craftability analysis.
- For item visuals, continue to use the normal tModLoader/Terraria rendering path (`Main.instance.LoadItem(...)`, `TextureAssets.Item`, `Main.itemAnimations`, `Item.SetDefaults(...)`), but add explicit bounds/readiness checks before indexing array-backed assets.
- If the first-open issue is caused by initialization order rather than a bad item ID, defer the unsafe step until the UI state is attached and ready instead of allowing a first-frame exception. Any deferral must stay internal to the analyzer UI and must not require extra player input.
- Preserve existing `AnalysisResult` contents, existing filter/search/sort logic, and the current analyzer layout. This task is about resilient UI presentation, not a redesign.
- Keep behavior compatible with large mod packs by deriving all decisions from runtime item metadata and current array lengths or loader counts, never from hardcoded IDs.

## UI/UX
- No layout or wording changes are required.
- The analyzer should look the same to the player except that the first-open error disappears.
- If a specific item icon cannot be drawn safely in a given frame, the slot or tree row should still show its text and remain usable rather than collapsing the whole UI interaction.

## Success Criteria
- [ ] Opening `Analyze Recipes` from the Steroid Guide in a fresh world session no longer prints `Index was outside the bounds of the array` to chat.
- [ ] The same fix works when the craftable results include modded items from other content mods.
- [ ] The top-tier item grid and recipe tree can both render after the first open without client-side exceptions.
- [ ] Search, sort, pagination, selection, and recipe-tree interactions still work after the fix.
- [ ] No item-ID hardcoding or mod-specific compatibility shims are introduced.

## Out of Scope
- Changing category definitions, search UX, pagination visuals, or recipe-tree styling.
- Rewriting the recursive recipe analyzer or top-tier filtering rules.
- Adding telemetry, config toggles, or a separate user-facing error UI for analyzer failures.
