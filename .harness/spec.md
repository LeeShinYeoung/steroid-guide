# Spec: Fix Craftable Item Eligibility and Recipe Tree Consistency

## Summary
Ensure the craftable item list only contains items the player can produce through an actual recipe chain from the currently scanned inventory and nearby chests. Fix the recipe tree so selecting a listed item always shows its crafting path even when the player already owns one or more copies of that result item.

## Detailed Requirements
1. Top-tier item analysis must treat the selected result item as something to craft, not something already satisfied by direct ownership. Existing copies of the root result item must not make that item eligible for `AllCraftable` or `TopTierItems`.
2. Intermediate ingredients must continue to use currently owned stacks exactly as they do today. The ownership exclusion applies only to the root result item being evaluated for list inclusion or tree expansion.
3. Items that are merely owned, but cannot currently be produced through any valid recipe chain, must be excluded from the craftable item list even if they have recipes registered in `Main.recipe[]`.
4. Items that are both already owned and still craftable from the current materials must remain in the craftable item list.
5. Selecting an item from the craftable item list must build a non-empty recipe tree whenever a valid crafting path exists. The root item being owned must not cause the tree to collapse to a single node.
6. The recipe tree must use the same craftability rule as list analysis so the selected item’s status, children, and alternative recipe behavior stay consistent with the analyzer result.
7. The fallback tree shown for missing paths must continue to surface missing ingredients and recipe conditions for debugging the path, but it must not rely on root-item ownership to mark a result as craftable.
8. The fix must remain data-driven and mod-compatible. No hardcoded item IDs, recipe IDs, or mod-specific exceptions are allowed.

## Technical Design
- Modify [Common/RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs) to separate two concepts that are currently conflated:
  - satisfying a requirement from owned stacks
  - producing a new copy of the target item through a recipe
- Introduce an internal evaluation path for root-item analysis that ignores owned copies of the root result item while still allowing owned intermediate ingredients to satisfy recursive requirements. The implementation can be a dedicated top-level method or a structured evaluation result passed into `CanCraft(...)`, but it must make the root/child distinction explicit.
- Keep `AnalysisResult.AllCraftable` and `AnalysisResult.TopTierItems` semantics aligned with the mod spec: membership means “craftable via recipe now,” not “owned or craftable.”
- Update the top-tier filter in [Common/RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs) to continue using the recipe-produced craftable set after the eligibility fix; no change to `RecipeGraphSystem.ItemUsedInResults` is required.
- Update [Common/UI/RecipeAnalyzerUIState.cs](/Users/sy/projects/steroid-guide/Common/UI/RecipeAnalyzerUIState.cs) so `OnItemSelected(...)` requests a recipe tree using the same root-item rule as the analyzer. The selected root item may still display its owned count for context, but tree expansion must be based on recipe viability, not the early owned short-circuit.
- Update [Common/RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs) `BuildRecipeTree(...)` so the root node can show recipe children and `UsedRecipe` even when `OwnedCount >= RequiredCount`, while child nodes continue to return `Owned` when direct ownership satisfies the requirement.
- Ensure recipe swapping in [Common/UI/UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) rebuilds children with the same root-evaluation rule so swapping does not reintroduce the empty-tree behavior.
- No changes are required to [Common/RecipeGraphSystem.cs](/Users/sy/projects/steroid-guide/Common/RecipeGraphSystem.cs) DAG construction, [Common/ItemScanner.cs](/Users/sy/projects/steroid-guide/Common/ItemScanner.cs) chest scanning, or any tModLoader hook usage beyond the existing `ModSystem.PostAddRecipes()`, UI `OnShow()`, and UI selection/update flow.

## UI/UX
- No layout redesign is required.
- The existing item list, pagination, and recipe-tree presentation remain unchanged.
- The user-facing behavioral change is:
  - items already owned but not currently craftable disappear from the craftable list
  - items already owned and still craftable remain selectable and show their recipe tree normally

## Success Criteria
- [ ] With wood in inventory and iron bars in a nearby scanned chest, `Chest` appears in the craftable list only if it is actually craftable through its recipe, and selecting it shows the expected recipe tree instead of a single root node.
- [ ] Owning a final weapon or tool without the remaining ingredients to craft another copy does not cause that item to appear in `TopTierItems`.
- [ ] Owning one or more copies of a result item does not prevent the UI from showing its crafting station row and ingredient children when a valid recipe path still exists.
- [ ] Alternative recipe swapping preserves correct `Craftable` vs `Missing` child statuses under the new root-item rule.

## Out of Scope
- Expanding the scanned storage sources beyond current inventory and on-screen chests
- Reworking category filters, sorting, search, or pagination behavior
- Adding new UI chrome, badges, or redesigning the recipe tree visuals
- Performance optimizations beyond the refactor needed to make root craftability explicit
