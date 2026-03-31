# Build Report

## Implemented
- [x] Stabilized the Steroid Guide body rendering by using the vanilla Guide town-NPC body texture/profile path for all draw states.
- [x] Sourced the Steroid Guide animation frame metadata directly from `NPCID.Guide` runtime values instead of hand-maintained literals.

## Files Changed
- `Content/NPCs/SteroidGuideNPC.cs` — replaced the bespoke body profile with the vanilla Guide `LegacyNPCProfile`, aligned the body texture path with that profile, and copied Guide animation metadata from runtime values.

## How to Test
1. Build with tModLoader.
2. Spawn or find the Steroid Guide in-game and watch the NPC while idle, walking, and turning.
3. Confirm the full body always matches the vanilla Guide art without sliced or stretched animation frames.
4. Talk to the Steroid Guide, verify the custom head icon still appears in housing/chat UI, and click `Analyze Recipes`.
5. While the analyzer is open, confirm the NPC still freezes in place until the interaction closes.

## Known Issues
- None observed.
