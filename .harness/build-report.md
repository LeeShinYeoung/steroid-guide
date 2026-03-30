# Build Report

## Implemented
- [x] Guarded analyzer item-icon rendering so first-open UI frames no longer index item asset arrays blindly.
- [x] Applied the same safe visual handling to both the top-tier item grid and recipe tree rows while preserving text, hover, and click behavior.

## Files Changed
- `Common/UI/UIItemRenderingHelper.cs` — added a shared helper for safe item creation and guarded icon drawing.
- `Common/UI/UIItemGrid.cs` — routed grid names, hovers, and icons through the guarded helper.
- `Common/UI/UIRecipeTree.cs` — routed recipe-tree names, hovers, and icons through the guarded helper.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Launch Terraria with Steroid Guide enabled, enter a world, talk to the Steroid Guide NPC, and click `Analyze Recipes` immediately after world load.
3. Confirm no `Index was outside the bounds of the array` message appears in chat on first open or later opens.
4. Verify the craftable item grid still renders, hovering items still shows tooltips, and selecting an item still opens the recipe tree.
5. Verify recipe-tree rows render without exceptions, and search, sorting, pagination, collapse, and alternative recipe switching still behave normally.

## Known Issues
- Manual in-game verification was not run in this environment.
