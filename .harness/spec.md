# Spec: Crafting Station Icon-First Recipe Tree UI

## Summary
Replace the recipe tree's plain-text crafting-station lines with an icon-first presentation that is faster to scan and easier to understand. Players should be able to recognize required stations at a glance without losing clarity about what the icons mean.

## Detailed Requirements
1. Every recipe tree node that currently shows crafting-station text must render a dedicated station row for its active `Recipe`, including the root recipe and expanded child recipes.
2. The station row must communicate "this recipe needs a crafting station" without relying on a long `"Crafting Station: ..."` sentence. Short supporting text is allowed, but the primary affordance must be graphical.
3. Each required station in `Recipe.requiredTile` must be represented individually. Recipes with multiple required tiles must show multiple station entries in the same row or grouped station block.
4. Station visuals must be derived dynamically from game data and remain compatible with modded content. The implementation must not hardcode vanilla or mod item IDs.
5. When a representative item icon can be resolved for a required tile, that icon must be drawn using the existing item-icon rendering path so vanilla/modded station items look consistent with the rest of the UI.
6. When no representative item icon can be resolved, the station row must still render a stable fallback using the resolved tile name, and the UI must not fail or leave the station area blank.
7. Hovering a station icon or fallback entry must expose the station name clearly, either through tooltip behavior or adjacent label treatment, so players do not have to guess what a station icon means.
8. The station row must respect the current recipe-tree indentation and scrolling model. It must remain visually associated with the node whose recipe it describes and must not break connector-line rendering or `UIList` scrolling.
9. Alternative-recipe swapping must refresh the station presentation immediately so the displayed station set always matches the currently active `UsedRecipe`.
10. Existing condition lines and alternative-recipe controls must continue to work. This change is limited to the crafting-station presentation, not recipe logic.

## Technical Design
- Modify `Common/UI/UIRecipeTree.cs` to replace the current text-only `AddCraftingStationLine(...)` flow with a richer tree child element that can render a compact station badge/row with icons plus short context text.
- Add a small station-display model or helper within the UI layer to resolve, per required tile:
  - tile display name via the existing `TileLoader.GetTile(...)` / `MapHelper.TileToLookup(...)` / `Lang.GetMapObjectName(...)` fallback chain
  - representative item id by scanning valid item definitions and matching `Item.createTile` to the required tile id
- Cache the tile-to-item resolution so recipe-tree redraws and alternative-recipe swaps do not rescan the full item set every frame.
- Reuse `UIItemRenderingHelper.TryDrawItemIcon(...)` and `TryCreateDisplayItem(...)` for icon drawing and hover item setup so station icons inherit the same safety checks already used by item rows.
- Keep the new presentation in the existing UI layer only. No changes are required in `RecipeAnalyzer`, `RecipeGraphSystem`, `ItemScanner`, NPC logic, or world systems because the feature is purely presentational.
- Preserve current tree behaviors:
  - `Recipe.requiredTile` remains the source of required stations
  - `UIList`/`UIScrollbar` still handle vertical scrolling
  - `SwapAlternativeRecipe(...)` and `SetTree(...)` remain the re-render path after recipe changes
- Likely files:
  - `Common/UI/UIRecipeTree.cs` for the new station row element and integration
  - `Common/UI/UIItemRenderingHelper.cs` only if a small shared hover/icon helper is needed for station entries
  - `Localization/en-US_Mods.SteroidGuide.hjson` only if a short reusable station label is introduced

## UI/UX
- Replace the current sentence-style station line with a compact station row that reads as a labeled group, such as a small "Station" caption plus one or more framed item icons.
- Keep text secondary: icons should carry most of the recognition burden, while hover or a short caption provides disambiguation.
- Use spacing, tint, or a subtle container so the station block feels intentional and distinct from ingredient nodes, but still belongs to the same recipe-tree branch.
- For multiple stations, present them as a tidy horizontal sequence instead of comma-separated prose.

## Success Criteria
- [ ] Recipe tree entries no longer show raw `Crafting Station: ...` sentences for required stations; they show an icon-first station presentation instead.
- [ ] Vanilla and modded crafting stations are resolved dynamically without hardcoded item ids.
- [ ] Recipes with multiple required stations display every station in the active recipe.
- [ ] Swapping to an alternative recipe updates the shown station icons/names immediately.
- [ ] If an icon cannot be resolved for a station, the UI still renders a clear fallback name and remains stable.

## Out of Scope
- Redesigning ingredient-node visuals, connector lines, or recipe-tree collapse behavior.
- Changing recipe analysis, craftability rules, or alternative-recipe selection logic.
- Adding new gameplay systems, new stations, or custom mod content.
