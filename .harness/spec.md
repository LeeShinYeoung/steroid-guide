# Spec: Persistent Alternative Recipe Toggle for Nested Recipe Tree Nodes

## Summary
Fix the recipe tree so child nodes with multiple recipes behave like the root node when players cycle alternatives. After switching a nested node to another recipe, the UI must keep showing that node's selected recipe details and the `Alternative Recipe` control instead of making the control disappear. This lets players inspect and compare alternate crafting paths without losing navigation state.

## Detailed Requirements
1. Any recipe-tree node, including non-root child nodes, must keep exposing an `Alternative Recipe` action whenever that node still has one or more entries in `AlternativeRecipes`.
2. Clicking `Alternative Recipe` on a child node must not permanently remove the control from the tree. The player must be able to click the same node's alternative toggle repeatedly to rotate through all available recipes.
3. After a child node switches to a different recipe, the tree must continue to render that node's currently selected recipe details: crafting stations, conditions, and ingredient children for `UsedRecipe`.
4. The visibility of a child node's recipe details and alternative toggle must not depend on `NodeStatus.Craftable`. If the newly selected recipe is currently missing ingredients, the node may show a missing status color, but its selected recipe subtree and alternative toggle must still remain visible while the node is expanded.
5. Collapse and expand behavior must continue to work for nested nodes, but collapse state must be based on whether the node has displayable recipe children, not only whether the node is currently craftable.
6. Root-node behavior must remain unchanged except for sharing the same rendering rules as child nodes where practical. Existing text, ordering, and click affordances for the root alternative toggle should be preserved.
7. The change must remain compatibility-safe for vanilla and modded recipes. Do not hardcode any item IDs, recipe counts, or item-specific exceptions.

## Technical Design
- Modify [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) so child-node rendering uses separate predicates for:
  - whether a node has recipe details to display (`UsedRecipe` plus built `Children`)
  - whether a node is currently collapsed
  - whether a node is currently craftable for status coloring
- Replace the current child-only gate that ties subtree rendering to `child.Status == NodeStatus.Craftable && child.Children.Count > 0`. A node with a selected `UsedRecipe` and populated `Children` should still render its subtree and alternative-switch action even when its status is `Missing`.
- Keep the triangle collapse affordance for nodes with displayable children, but do not suppress it solely because a recipe path is not currently craftable.
- Update `SwapAlternativeRecipe(...)` in [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) to keep rebuilding `Children` from the new `UsedRecipe`, recompute the node status from the rebuilt children, and then re-render without losing nested alternative-toggle eligibility.
- The existing `RecipeTreeNode` contract in [RecipeAnalyzer.cs](/Users/sy/projects/steroid-guide/Common/RecipeAnalyzer.cs) already carries `UsedRecipe`, `Children`, and `AlternativeRecipes`; no recipe-graph changes are expected. If Generator needs a small helper or naming cleanup to make “displayable recipe details” explicit, keep it local to the recipe-tree pipeline.
- No changes are expected in inventory scanning, top-tier analysis, pagination, search, filters, or NPC interaction. The work is scoped to recipe-tree presentation logic and any minimal supporting data flow needed to preserve the selected nested recipe state.

## UI/UX
- Nested nodes should behave consistently with the root: status row first, then the selected recipe's station/condition rows and ingredient subtree, then the `Alternative Recipe` control.
- Switching a child node to a non-craftable alternative should still give the player useful information by leaving the missing recipe path visible instead of collapsing it into a dead end.
- The visual style of the alternative toggle, status colors, and collapse triangles should remain consistent with the current custom tree UI.

## Success Criteria
- [ ] A child node with multiple recipes still shows an `Alternative Recipe` control after the player clicks it once.
- [ ] Repeated clicks on the same child node rotate through available recipes without the control disappearing.
- [ ] When a switched child recipe is not currently craftable, that node still shows its selected recipe subtree and the player can switch again.
- [ ] Root alternative-recipe behavior and existing tree interactions remain intact.

## Out of Scope
- Redesigning the overall recipe-tree layout or wording
- Adding new recipe-tree filters, localization keys, or item metadata
- Changing recipe analysis results, scanner behavior, or top-tier item selection rules
