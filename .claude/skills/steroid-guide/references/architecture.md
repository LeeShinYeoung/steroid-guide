# Steroid Guide — Architecture Reference

## Overview

Steroid Guide is a tModLoader mod for Terraria that adds a town NPC ("Steroid Guide") who analyzes the player's inventory and nearby chests to show all craftable items, ranked by tier, with interactive recipe tree visualization.

## Tech Stack
- C# / .NET 8.0 / tModLoader (Terraria modding framework)
- XNA Framework (MonoGame) for rendering
- Custom pixel-based UI (all shapes drawn with `TextureAssets.MagicPixel`, no texture assets)

## Project Structure

```
SteroidGuide/
├── SteroidGuideMod.cs              — Mod entry point, packet handling (MessageType dispatch)
├── Common/
│   ├── RecipeGraphSystem.cs        — Builds recipe dependency graph at PostAddRecipes
│   ├── CraftableAnalyzer.cs        — Recursive recipe tree traversal & craftability analysis
│   ├── ItemScanner.cs              — Scans player inventory + nearby chests (60-tile radius)
│   ├── ItemCategoryClassifier.cs   — Classifies items into FilterCategory enum
│   ├── ChestSyncSystem.cs          — Server-side progressive chest sync for multiplayer
│   └── UI/
│       ├── CraftableUISystem.cs         — UI lifecycle (ModSystem), input handling
│       ├── CraftableUIState.cs          — Main UI state: OnInitialize, Update, layout constants
│       ├── CraftableUIState.Filtering.cs — Filter/sort/search/pagination logic
│       ├── CraftableUIState.Analysis.cs  — Scan triggering, analysis, CachedItemProps
│       ├── UIRecipeTree.cs              — Collapsible recipe tree with station badges
│       ├── UIItemGrid.cs               — 12x3 item grid with selection + hover
│       ├── UISearchTextBox.cs           — Text input with caret animation
│       ├── UISelectableOption.cs        — Radio-button filter option
│       ├── UISortButton.cs             — Sort dropdown trigger with 3-line icon
│       ├── UICloseButton.cs            — X button (pixel-drawn diagonal glyph)
│       ├── UICenteredText.cs           — Centered text element (pagination label)
│       ├── UIPaginationArrowButton.cs  — Stepped-triangle page arrows
│       ├── UIDrawHelper.cs             — DrawRect + DrawBorder (MagicPixel wrappers)
│       └── UIItemRenderingHelper.cs    — Item icon drawing + safe name/texture resolution
├── Content/
│   ├── NPCs/SteroidGuideNPC.cs         — Town NPC: chat, bestiary, "Craftable" button
│   ├── Players/SteroidGuideModPlayer.cs — Death tracking for NPC dialogue
│   └── World/SteroidGuideWorldGen.cs    — Spawn NPC at world generation
├── Localization/
│   └── en-US_Mods.SteroidGuide.hjson   — All UI strings + NPC dialogue
├── build.txt                            — Mod metadata (displayName, author, version)
└── SteroidGuide.csproj                  — .NET 8.0, tModLoader targets
```

## Subsystem Details

### 1. Recipe Analysis (Core Algorithm)

**Files:** `RecipeGraphSystem.cs`, `CraftableAnalyzer.cs`

`RecipeGraphSystem` builds a graph once at `PostAddRecipes`:
- `RecipesByResult[itemId]` → `List<Recipe>` (all recipes producing this item)
- `ItemUsedInResults[itemId]` → `HashSet<int>` (all result items using this ingredient)

`CraftableAnalyzer` provides two modes via unified `TraverseRecipes`:
- **Analysis mode** (`consumeAvailable=true`): Mutates `available` dict, uses `noRecipeCache`, breaks early on missing. Used by `Analyze()` to determine all craftable items.
- **Display mode** (`consumeAvailable=false`): Read-only, builds full tree with fallback recipe. Used by `BuildRecipeTree()` for UI rendering.

**Critical mechanics:**
- Cycle detection via `visiting` HashSet (add before recurse, remove after)
- State rollback via `DictSnapshot` using `ArrayPool<(int,int)>.Shared` (must Rent/Return properly)
- Top-tier filtering: craftable items not used as ingredient for another craftable item
- Batch calculation: `batches = (remaining + batchSize - 1) / batchSize`

### 2. Item Scanning & Multiplayer Sync

**Files:** `ItemScanner.cs`, `ChestSyncSystem.cs`, `SteroidGuideMod.cs`

`ItemScanner.ScanAvailableItems` collects:
- Player inventory slots 0-57 (hotbar + inventory + coins + ammo)
- Nearby chests within 60-tile radius (960px, squared distance comparison)

**Multiplayer sync protocol:**
1. Client scans → finds unsynced chest → sends `RequestChestContents` (max 8/scan, tracked in `_requestedChests`)
2. Server receives → `ChestSyncSystem.Enqueue(chestIndex, toClient)`
3. Server `PostUpdateWorld` → dequeues, sends `SyncChestItem` packets progressively (max 40/frame)
4. Server sends `ChestContentsReady` when all 40 slots sent
5. Client receives → `ItemScanner.MarkChestSynced(chestIndex)` with frame timestamp
6. Sync expires after `ChestSyncTTLFrames` (3600 = 60 seconds)

### 3. UI Layer

**Files:** `CraftableUISystem.cs`, `CraftableUIState` (3 partial files), all `UI*` components

**Lifecycle:**
- `CraftableUISystem.Load()` → create `UserInterface` + `CraftableUIState`
- `ShowUI(npcIndex)` → set state, close NPC chat, trigger initial analysis
- `UpdateUI` → every frame: scan items (every 30 frames), debounce analysis (30 frames), handle input
- `HideUI()` → on ESC, NPC out of range, or close button

**UI layout (820x600 centered panel):**
- Left sidebar (120px): 9 filter categories + sort dropdown
- Content column: search box (32px) → item grid 12x3 → pagination → recipe tree (228px)
- Recipe tree: collapsible nodes with tree connector lines, crafting station badges (icon or text fallback)

**Rendering:** Every UI element draws itself via `DrawSelf(SpriteBatch)` using `TextureAssets.MagicPixel` stretched into rectangles. No texture files for UI components.

**Analysis debounce:**
- Scan runs every 30 frames in `Update`
- If scan result changed: set `_analysisPending = true`, reset `_analysisDebounceTimer = 30`
- Timer counts down each frame; at 0, runs `RunAnalysisFromLatestScan()`
- Prevents rapid re-analysis during multiplayer chest sync bursts

### 4. Content Layer

**Files:** `SteroidGuideNPC.cs`, `SteroidGuideModPlayer.cs`, `SteroidGuideWorldGen.cs`

- NPC uses vanilla Guide animations (`AnimationType = VanillaGuideType`)
- Chat button "Craftable" → `CraftableUISystem.ShowUI(NPC.whoAmI)`
- NPC freezes while UI open via vanilla `talkNPC` mechanism (server-synced, multiplayer-safe)
- `CraftableUISystem` keeps `talkNPC` set while custom UI is visible, suppresses vanilla chat layer
- Auto-close UI if player moves >300px from NPC, or if vanilla clears `talkNPC` independently
- `ModPlayer` tracks death for 60s (`DeathDialogueDuration = 3600 frames`) for special dialogue

## Data Flow

```
[World Load] → PostAddRecipes → RecipeGraphSystem.BuildGraph()

[NPC Button] → ShowUI() → OnShow() → ScanAvailableItems() → Analyze() → TopTierItems
                                                                             ↓
                                                              Filter/Sort → ItemGrid display
                                                                             ↓
                                                              [Click item] → BuildRecipeTree() → UIRecipeTree

[Every 30 frames] → Scan → changed? → debounce 30 frames → re-analyze → refresh UI
```

## tModLoader Lifecycle Hooks

| Hook | System | Purpose |
|------|--------|---------|
| Load | CraftableUISystem | Create UserInterface + CraftableUIState |
| Unload | CraftableUISystem | Null UI refs, clear caches (TileDisplayItemCache) |
| Unload | RecipeGraphSystem | Null Graph |
| PostAddRecipes | RecipeGraphSystem | Build recipe graph (runs once after all mods loaded) |
| UpdateUI | CraftableUISystem | Frame update, ESC/Enter handling, NPC distance check |
| PostUpdateInput | CraftableUISystem | Search text capture, scroll wheel lock |
| ModifyInterfaceLayers | CraftableUISystem | Insert draw layer before "Vanilla: Mouse Text" |
| PostUpdateWorld | ChestSyncSystem | Process server-side chest sync queue |
| OnWorldUnload | CraftableUISystem | Clear ItemScanner sync state |
| OnWorldUnload | ChestSyncSystem | Clear sync queue |
| PostWorldGen | SteroidGuideWorldGen | Spawn NPC at world spawn point |
| Kill | SteroidGuideModPlayer | Set RecentlyDied flag |
| PostUpdate | SteroidGuideModPlayer | Countdown death timer |
| PreAI | SteroidGuideNPC | Freeze NPC while UI is open |
