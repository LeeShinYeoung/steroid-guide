# Changelog

Steam Workshop Change Notes, newest first.
When publishing a new version, copy the BBCode block for that release into the **Change Notes** field during upload.

---

## v1.1.3

```
[h2]v1.1.3 — Magic Storage Load Guard[/h2]

[h3]Bug Fixes[/h3]
[list]
[*]Fixed an issue with optional Magic Storage support
[/list]
```

---

## v1.1.2

```
[h2]v1.1.2 — Rarity Colors & UI Polish[/h2]

[h3]Visual[/h3]
[list]
[*]Item names in the craftable grid, recipe tree, and ingredient rows are now colored by rarity, matching Terraria's tooltip color scheme
[*]Recipe tree connector lines are brighter, so parent-child structure reads more clearly at a glance
[*]Leaf ingredient rows have a more visible row background, making the have/need column easier to scan
[*]The craftable panel's opacity now matches the NPC chat dialog, so the two windows blend consistently when both are open
[/list]

[h3]Layout Tweaks[/h3]
[list]
[*]Item grid is now 5 columns with larger icons and refined name padding
[*]Pagination buttons share the same palette as sort buttons for a more uniform header
[*]Titlebar simplified — status pill background/border removed, close button shrunk and centered
[/list]

[h3]Bug Fixes[/h3]
[list]
[*]Fixed an issue where recipe tree rows would occasionally appear in scrambled order due to UIList's implicit sort
[/list]
```

---

## v1.1.1

```
[h2]v1.1.1 — Magic Storage & Personal Bank Support[/h2]

[h3]New Sources[/h3]
[list]
[*]Magic Storage is now recognized — items inside any nearby Storage Heart count toward the craftable list, just like a regular chest
[*]Multiple separate storage networks within range are all included (e.g. main warehouse + dedicated ore warehouse)
[*]Piggy Bank, Safe, Defender's Forge, and Void Vault contents are now always included, regardless of where you are
[/list]

[h3]Notes[/h3]
[list]
[*]Magic Storage support is optional — the mod works the same as before if Magic Storage isn't installed
[*]Each Storage Heart counts as one entry in the "nearby chests" indicator
[/list]
```

---

## v1.1.0

```
[h2]v1.1.0 — Crafting UI Redesign & Recipe Tree Rework[/h2]

[h3]UI Redesign[/h3]
[list]
[*]The crafting window is now a 3-column vertical layout — categories on the left, item grid in the middle, recipe tree on the right
[*]New dedicated titlebar and a unified color palette for a more consistent look
[*]Each category now shows the number of currently craftable items as a badge
[*]The item grid is reshaped from wide-and-short to tall-and-narrow, with larger icons for better readability
[/list]

[h3]Recipe Tree[/h3]
[list]
[*]Parent-child connectors have been replaced — ASCII-style branches are gone, in favor of a single vertical line that binds siblings under the same parent, with a short horizontal stub branching out to each child
[*]Item icons now sit in bordered tiles, and every depth step uses a uniform indent so rows line up cleanly regardless of how deep the tree goes
[*]Intermediate ingredients no longer show a "CRAFTABLE" chip — instead, your current owned count is shown on the right edge, aligned with the leaf ingredients' have/need so you can compare at a glance
[*]Removed the "RECIPE TREE" header bar — the tree now fills the full column height
[/list]

[h3]Bug Fixes[/h3]
[list]
[*]Fixed a confusion where "Conditions: …" lines (shimmer, transmutation, etc.) appeared as if they were separate craftable recipes — these lines are temporarily hidden until a clearer UX is ready
[*]Fixed an issue where the page and the selected item would reset every time the background analysis re-ran while you were talking to the NPC
[*]Fixed an issue where a nearby chest's sync cache expiring would briefly treat that chest as empty, causing the craftable list to shrink until re-sync completed
[/list]
```

---

## v1.0.7

```
[h2]v1.0.7 — Deep Search Across Visible Recipe Tree[/h2]

[list]
[*]Search now matches items anywhere in a craftable's visible recipe tree, not just the top-tier item's own name
[*]Typing a raw material name (e.g. "wood") now surfaces every finished item that currently needs it — "Wooden Sword", "Work Bench", and more
[*]Deep search stays consistent with what's on screen — it only matches against ingredients the tree actually shows, so owned intermediates don't produce phantom results
[/list]
```

---

## v1.0.6

```
[h2]v1.0.6 — NPC Sprite Refresh & Animation Fix[/h2]

[h3]Visual[/h3]
[list]
[*]Redesigned NPC sprite with new walk/idle/attack frames
[*]Refreshed Workshop thumbnail icon
[/list]

[h3]Bug Fixes[/h3]
[list]
[*]Fixed sprite tearing during walking and talking animations
[/list]

[h3]Workshop Page[/h3]
[list]
[*]Added an intro line clarifying the mod adds a town NPC
[*]Added an "Arrival" section (spawns at world spawn, no condition)
[*]Added a compatibility note (Calamity and more)
[*]Added GitHub link
[/list]
```

---

## v1.0.5

```
[h2]v1.0.5 — Remove Analysis Debounce[/h2]

[list]
[*]Removed the 1.5-second "queued for update" delay after your inventory or nearby chests change
[*]Analysis now starts immediately when the scan changes, so the craftable list updates without waiting
[/list]
```

---

## v1.0.4

```
[h2]v1.0.4 — Background Analysis for Large Chest Scenarios[/h2]

[h3]Performance[/h3]
[list]
[*]Eliminated the main-thread stutter that happened when opening the NPC's UI near many chests in multiplayer (around 80 chest scenario)
[*]Analysis now runs in the background, so the game stays responsive while the craftable list is being built
[*]Increased chest sync rate limits, so large chest scenarios saturate faster and analysis stabilizes in fewer passes
[/list]

[h3]UX[/h3]
[list]
[*]New unified progress indicator at the top of the UI — tells you whether the NPC is syncing chests, analyzing, waiting, or idle
[/list]
```

---

## v1.0.3

```
[h2]v1.0.3 — Multiplayer & Stability Fixes[/h2]

[h3]Bug Fixes[/h3]
[list]
[*]Stabilized multiplayer networking — smoother chest sync with fewer dropped packets
[*]Fixed the NPC walking away mid-conversation in multiplayer
[*]Fixed stale item cache after reloading mods
[*]Fixed sort-by-rarity for modded rarities (Calamity and others)
[/list]

[h3]Performance[/h3]
[list]
[*]Analysis debounced during chest sync to avoid repeated re-runs
[*]Reduced GC pressure via pooled inventory snapshots
[/list]
```

---

## v1.0

```
[h2]v1.0 — Craftable Analyzer UI & Codebase Refactor[/h2]

[h3]New Features[/h3]
[list]
[*]Keyword search — instant filtering of the craftable list by item name
[*]Category filter — sidebar to narrow results by weapons, armor, accessories, etc.
[*]Recipe tree improvements — nested alternative recipe toggles, icon-based crafting station display
[*]Pagination with mouse-wheel support
[*]Custom NPC sprite with stabilized animation
[*]Nearby chest reference count displayed while the UI is open
[/list]

[h3]Bug Fixes[/h3]
[list]
[*]Guarded against the first-open UI rendering exception
[*]Fixed search box input and placeholder behavior
[*]Fixed pagination arrow left/right mirroring
[*]Fixed craftable item eligibility consistency
[/list]
```
