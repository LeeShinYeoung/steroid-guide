# Steroid Guide

A smarter Guide NPC for Terraria. Instead of checking recipes one item at a time, Steroid Guide scans your entire inventory and nearby chests to show you every top-tier item you can craft — including multi-step recipes.

## Features

- **Recursive recipe analysis** — Traces full crafting chains automatically. If you have the raw materials for a Night's Edge (across multiple intermediate crafts), it tells you.
- **Top-tier item filtering** — Only shows final products, not intermediate steps. If Light's Bane is just a stepping stone to Night's Edge, only Night's Edge appears.
- **Inventory + chest scanning** — Reads your inventory and all chests visible on screen.
- **Category filters** — Filter results by Weapons, Armor, Accessories, Potions, Tools, or Misc.
- **Recipe tree viewer** — Click any item to see its full crafting tree with owned/craftable/missing status for each material.
- **Universal mod support** — Works with any mod's recipes (Calamity, Thorium, etc.) since it reads from `Main.recipe` at runtime.

## How It Works

1. **Graph build** (once on mod load) — Builds a directed acyclic graph from all registered recipes. Nodes are items, edges are "is material for" relationships.
2. **Item scan** (on UI open / inventory change) — Aggregates items from player inventory + on-screen chests into an `itemID → quantity` map.
3. **Recursive search** — For each item in the game, determines if it's craftable from available materials (directly owned or recursively craftable). Uses memoization and cycle detection.
4. **Top-tier filter** — From all craftable items, removes any that serve as materials for another craftable item. The remainder are your "best possible" crafts.

Recalculation only triggers when inventory or visible chests actually change.

## The NPC

Steroid Guide is a passive town NPC that spawns when a valid house is available. Talk to him and click **"Analyze Recipes"** to open the recipe analyzer UI.

He has opinions about the vanilla Guide.

## Installation

Requires [tModLoader](https://github.com/tModLoader/tModLoader) (net8.0).

Place the mod folder in `tModLoader/Mods/` or build from source.

## Compatibility

- Terraria 1.4.4+ with tModLoader
- .NET 8.0
- Compatible with all content mods — no hardcoded recipes
