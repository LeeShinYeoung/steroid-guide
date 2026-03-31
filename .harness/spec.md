# Spec: Icon-Only Crafting Station Row

## Summary
Remove the literal `Station` label from the recipe tree's crafting-station row so players only see the relevant crafting-station icon(s) or fallback station chip(s). This trims redundant text from the tree without changing which stations are required or how recipe details behave.

## Detailed Requirements
1. Every crafting-station row rendered in the recipe tree must omit the leading `Station` text badge and show only the required station badges for the selected recipe.
2. The change must apply consistently to the root recipe node and every expanded child recipe node that currently renders crafting-station information.
3. When a station can be resolved to a representative item, the row must continue to render the station as an icon badge exactly as today.
4. When no representative item can be resolved, the row must still render a readable fallback chip using the localized tile name or existing fallback name logic. Removing the `Station` label must not hide unidentified modded stations.
5. Station badge hover behavior must remain intact. Hovering an icon badge should still expose the station item tooltip when available, and hovering a fallback chip should still expose the resolved station name.
6. The station row must continue to wrap cleanly when multiple required stations exist. Removing the label must not create awkward left padding, clipping, or uneven vertical spacing.
7. Tree indentation and ancestor connector lines must remain visually aligned with the existing recipe-tree hierarchy after the label is removed.
8. Recipe condition text and alternative recipe controls must remain in their current order and behavior. This task removes redundant station labeling only; it does not redesign other recipe-detail rows.
9. The implementation must remain compatibility-safe for vanilla and modded recipes. No hardcoded station IDs, English-only station names, or special cases for specific crafting tiles may be introduced.

## Technical Design
- Modify [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) to stop passing or drawing the dedicated station label within `UITreeStationLine`.
- Update `AddCraftingStationLine(...)` in [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) so the line is created from the resolved station list alone, while leaving the existing `Recipe.requiredTile` traversal and condition-row rendering intact.
- Refactor `UITreeStationLine` in [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs) so layout width/height calculations start from the first badge, not from a leading label box. Keep the existing badge wrapping model, ancestor-line drawing, hover handling, and fallback text truncation behavior.
- Preserve the current station-resolution pipeline in [UIRecipeTree.cs](/Users/sy/projects/steroid-guide/Common/UI/UIRecipeTree.cs): `ResolveStations(...)`, `ResolveDisplayItemIdForTile(...)`, `GetTileName(...)`, and the tile-display-item cache should keep working exactly as they do now.
- Remove the now-unused `Mods.SteroidGuide.UI.RecipeTree.StationLabel` localization entry from [en-US_Mods.SteroidGuide.hjson](/Users/sy/projects/steroid-guide/Localization/en-US_Mods.SteroidGuide.hjson) unless the Generator finds another active consumer.
- Do not modify recipe analysis, selection state, pagination, or inventory scanning files. This is a rendering/layout change inside the recipe-tree station-detail row.

## UI/UX
- The crafting-station row should read as a compact strip of station badges directly beneath the owning recipe node.
- Single-station recipes should present one centered-left badge without a preceding text pill.
- Multi-station recipes should wrap into additional rows cleanly, with spacing that still feels connected to the recipe node above.
- Fallback text chips for unresolved stations may remain visually distinct from icon badges, but they should no longer be prefixed by a separate `Station` label.

## Success Criteria
- [ ] Recipe-tree crafting-station rows no longer show a `Station` text badge anywhere in the UI.
- [ ] Vanilla and modded recipes still show the correct station icon badges or readable fallback chips after the label removal.
- [ ] Hover tooltips/names, wrapping, indentation, and ancestor tree lines still behave correctly for root and nested recipe nodes.
- [ ] Recipe conditions and alternative recipe controls remain unchanged aside from shifting upward to use the reclaimed horizontal space.

## Out of Scope
- Redesigning condition rows, alternative recipe buttons, or other recipe-tree text
- Changing how crafting stations are resolved from `Recipe.requiredTile`
- Adding new icon art, textures, or richer station metadata
- Altering recipe analysis, craftability rules, filtering, sorting, or pagination
