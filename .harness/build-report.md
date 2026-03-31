# Build Report

## Implemented
- [x] Refreshed the analyzer sort trigger so it shows a compact graphical sort icon plus the active sort label without the old `Sort:` prefix or right-edge arrow affordance.
- [x] Replaced the sort dropdown's text-based `[ ]` / `[*]` markers with graphical option rows that match the left sidebar's hover and selected-state language while preserving the existing sort behavior and selection flow.

## Files Changed
- `Common/UI/RecipeAnalyzerUIState.cs` — swapped the old text-based sort UI to custom controls, centralized sort display labels, and kept the existing dropdown/select/apply flow intact.
- `Common/UI/UISortButton.cs` — added a custom icon-first sort trigger renderer with hover/open states and no trailing arrow region.
- `Common/UI/UISortOption.cs` — added full-row sort dropdown rows with graphical indicators styled to match the filter sidebar.

## How to Test
1. Build with tModLoader using `dotnet build -p:TModLoaderTargetsPath=/Users/sy/.local/share/tModLoader/tMLMod.targets`.
2. Open the Steroid Guide analyzer UI and inspect the sort control under the left filter sidebar.
3. Verify the collapsed trigger shows a graphical sort icon plus one of `Rarity`, `Name`, `Value`, or `Recipe Depth`, with no literal `Sort:` prefix or arrow-only button area.
4. Open the dropdown, confirm each row uses a graphical selected indicator instead of `[ ]` / `[*]`, and check that hover/selected states read consistently with the filter sidebar.
5. Select each sort mode and confirm the dropdown closes immediately and the result grid reorders using the existing sort semantics.

## Known Issues
- None.
