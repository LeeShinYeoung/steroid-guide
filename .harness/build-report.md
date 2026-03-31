# Build Report

## Implemented
- [x] Root craftability now ignores owned copies of the selected/result item while still consuming owned intermediate ingredients.
- [x] Craftable/top-tier list membership now means "craftable via recipe now" rather than "owned or craftable."
- [x] Selecting a listed item now builds its recipe tree with the same root-item rule, including alternative recipe swaps.

## Files Changed
- `Common/RecipeAnalyzer.cs` — separated root-item recipe evaluation from owned-stack satisfaction and carried the rule into tree construction metadata.
- `Common/UI/RecipeAnalyzerUIState.cs` — requests the selected item's recipe tree with root ownership ignored for craftability.
- `Common/UI/UIRecipeTree.cs` — preserves the same root-evaluation rule when swapping alternative recipes.

## How to Test
1. Build with tModLoader.
2. Open the Steroid Guide UI with a result item already owned but without enough ingredients to craft another copy, and confirm it does not appear in the craftable list.
3. Add the missing ingredients for that same result item, confirm it appears in the craftable list, then select it and verify the recipe tree shows stations and ingredient children instead of a single owned root.
4. Swap alternative recipes in the tree and confirm child craftable/missing states stay consistent.

## Known Issues
- None observed.
