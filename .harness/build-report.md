# Build Report

## Implemented
- [x] Kept nested recipe-tree nodes rendering their selected `UsedRecipe` details and `Alternative Recipe` action even after switching to a non-craftable alternative.
- [x] Replaced the recipe-tree `Alternative Recipe` text-glyph affordance with a dedicated graphical action row so root and nested toggles present as UI controls instead of clickable text characters.

## Files Changed
- `Common/UI/UIRecipeTree.cs` — kept persistent nested alternative-recipe behavior in place and converted the root/child `Alternative Recipe` rows into framed graphic action controls with a drawn chevron.

## How to Test
1. Build with tModLoader.
2. Open the Steroid Guide analyzer UI in-game and select an item whose recipe tree contains a child node with multiple recipes.
3. Verify the root and nested `Alternative Recipe` controls render as boxed action rows with a drawn arrow icon rather than a text `▶` glyph.
4. Expand a child node, click `Alternative Recipe`, and verify the same child still shows its station/condition rows, ingredient subtree, and `Alternative Recipe` control afterward.
5. Keep clicking the same child node's `Alternative Recipe` control and verify it rotates through the available recipes without the control disappearing.
6. Switch to an alternative recipe path that is currently missing ingredients and verify the node stays visible with missing-status coloring while still allowing another recipe switch.
7. Collapse and re-expand the child node and verify the triangle affordance still works based on whether the selected recipe has child ingredients to show.

## Known Issues
- None.
