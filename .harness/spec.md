# Spec: Balanced Sidebar Item Categories

## Summary
Redefine the analyzer's left sidebar categories so they better match how Terraria players think about items. The current logic classifies any damaging tool as a weapon, which makes pickaxes, hammers, and similar hybrid items show up under the wrong filter. This change introduces a more balanced category set and a deterministic classification priority so the sidebar feels trustworthy without exploding into overly fine-grained buckets.

## Detailed Requirements
1. Replace the current category set with this order: `All`, `Weapons`, `Armor`, `Accessories`, `Tools`, `Consumables`, `Placeables`, `Materials`, `Misc`.
2. `All` must remain the default selection whenever the analyzer UI opens.
3. Tool-class items must never appear under `Weapons` just because they have damage. At minimum, items with `pick > 0`, `axe > 0`, `hammer > 0`, or `fishingPole > 0` must resolve to `Tools`.
4. Classification must use a deterministic first-match priority so hybrid Terraria items land in a consistent bucket. The required precedence is: `Armor` -> `Accessories` -> `Tools` -> `Weapons` -> `Placeables` -> `Consumables` -> `Materials` -> `Misc`.
5. `Weapons` must cover combat-focused items detected through gameplay properties such as damage output or ammo behavior, but only after the higher-priority equipment/tool checks have run.
6. `Consumables` must replace the old `Potions` bucket and include non-placeable one-time-use utility items such as potions, buff items, healing or mana restoratives, food-like consumables, and similar consumable progression items.
7. `Placeables` must capture crafted items whose primary use is placing tiles or walls in the world, so common building outputs do not get lumped into `Misc` or `Consumables`.
8. `Materials` must capture crafting materials via Terraria item metadata when they are not already classified into a higher-priority bucket.
9. The sidebar UI must continue to behave as a single-select filter and must still combine correctly with search, sorting, pagination resets, and selected-item clearing.
10. Category labels shown in the UI must be localizable rather than relying on raw enum names.
11. The analyzer must keep full mod compatibility: classification rules may use `Item` properties and other tModLoader/Terraria metadata, but must not hardcode vanilla or mod item IDs.
12. The sidebar layout may be resized or vertically reflowed as needed to fit the extra categories, but the main analyzer panel must remain usable at the existing 820x600 footprint.

## Technical Design
- Modify `Common/UI/RecipeAnalyzerUIState.cs` to replace the old enum/order assumptions, build the sidebar from an explicit ordered category definition list, and keep `ApplyFilter()` behavior intact.
- Extract category classification out of UI-only string logic into a dedicated helper such as `Common/ItemCategoryClassifier.cs` so the rule order is centralized and testable by inspection.
- Update the `FilterCategory` enum to match the new buckets and keep `All` as the UI-only bypass case.
- Implement classification using Terraria `Item` fields populated by `Item.SetDefaults(itemId)`, including:
  - `headSlot`, `bodySlot`, `legSlot`
  - `accessory`
  - `pick`, `axe`, `hammer`, `fishingPole`
  - `damage`, `ammo`
  - `createTile`, `createWall`
  - `potion`, `buffType`, `healLife`, `healMana`, `consumable`
  - `material`
- Keep the classifier ordered exactly as specified so ambiguous items resolve consistently without per-item exceptions.
- Add localization keys in `Localization/en-US_Mods.SteroidGuide.hjson` for each filter label and any revised empty-state wording that references the new category set.
- If the additional rows no longer fit in the current sidebar block, adjust the filter panel height and the sort-control anchoring in `RecipeAnalyzerUIState.OnInitialize()` without changing the search box, item grid, or recipe tree behavior.
- Do not modify recipe analysis, chest scanning, pagination mechanics, or NPC behavior for this task.

## UI/UX
- The left sidebar should present the new category list in the exact order above.
- The new buckets should feel player-facing rather than engine-facing: a player looking for tools, blocks, or crafting materials should be able to guess the correct tab without learning internal Terraria flags.
- Switching categories must still update the item grid immediately, and items that disappear because of the new classification or current search query must clear the recipe tree selection the same way they do today.

## Success Criteria
- [ ] Pickaxes, axes, hammers, and fishing poles no longer appear in the `Weapons` filter solely because they deal damage.
- [ ] The sidebar exposes `Consumables`, `Placeables`, and `Materials` in addition to the existing equipment-oriented filters.
- [ ] Hybrid items resolve according to the documented priority order instead of arbitrary check order.
- [ ] Search, sort, page resets, and recipe-tree clearing continue to work after changing categories.
- [ ] No hardcoded item IDs are introduced; categorization remains metadata-driven and mod-compatible.

## Out of Scope
- Redesigning the recipe tree, pagination controls, search behavior, or NPC dialogue.
- Adding per-subtype combat tabs such as melee/ranged/magic/summon.
- Introducing bespoke overrides for individual vanilla or modded items whose metadata is unusual.
