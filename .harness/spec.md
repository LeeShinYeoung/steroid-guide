# Spec: Stabilize Steroid Guide NPC Animation Texture

## Summary
Fix the Steroid Guide NPC so the body sprite stays visually correct while moving, animating, and opening dialogue. Players should see the same clean vanilla Guide body art in motion that they already see when the NPC is idle, without losing Steroid Guide's custom head icon, dialogue, or analyzer interaction.

## Detailed Requirements
1. The Steroid Guide's body sprite must render without corruption in normal town-NPC animation states, including idle, walking, turning, and other Guide-style framed movement handled by vanilla animation logic.
2. The fix must preserve the current Steroid Guide behavior set: passive town NPC stats, custom names, custom dialogue, happiness, world-spawn behavior, and the `Analyze Recipes` chat button.
3. The NPC must continue using the mod's custom head icon for map/chat housing UI while the full body continues to visually match the vanilla Guide.
4. The animated body rendering path must be driven from vanilla Guide data rather than duplicated magic numbers. Frame-count and related animation metadata must come from `NPCID.Guide` runtime values or an equivalent vanilla-backed source.
5. The implementation must use one consistent body-texture/profile strategy for every animated draw state. It must not rely on a partially copied setup where idle uses a valid Guide texture path but animated frames resolve against mismatched metadata or a conflicting texture source.
6. The fix must not introduce hardcoded frame rectangles, manual per-frame offsets, or mod-specific item/NPC assumptions that would make future tModLoader updates brittle.
7. No recipe-analysis, UI, chest scanning, or dialogue-selection logic should change as part of this work unless a small refactor is strictly necessary to keep the NPC rendering path clean.

## Technical Design
- Modify [Content/NPCs/SteroidGuideNPC.cs](/Users/sy/projects/steroid-guide/Content/NPCs/SteroidGuideNPC.cs) as the primary implementation surface. This file currently mixes `ModNPC.Texture`, `ITownNPCProfile.GetTextureNPCShouldUse`, explicit town-NPC frame metadata, and `AnimationType = NPCID.Guide`.
- In `SetStaticDefaults()`, stop treating Guide animation metadata as hand-maintained literals. Source `Main.npcFrameCount[Type]`, `NPCID.Sets.ExtraFramesCount[Type]`, `NPCID.Sets.AttackFrameCount[Type]`, and `NPCID.Sets.HatOffsetY[Type]` directly from the corresponding `NPCID.Guide` values so Steroid Guide stays aligned with the vanilla Guide spritesheet layout.
- Review `Texture`, `TownNPCProfile()`, and `SteroidGuideProfile.GetTextureNPCShouldUse(...)` as one rendering contract. The final implementation should designate a single authoritative Guide body texture source for the NPC's full-body draw path and ensure animated frames resolve against that same source.
- Keep the mod head registration path intact through `HeadTexture`, `[AutoloadHead]`, and `ModContent.GetModHeadSlot(...)`. The fix is for body animation corruption, not for replacing the custom housing/chat head asset.
- Continue using vanilla Guide animation framing through `AnimationType = NPCID.Guide` rather than introducing custom `FindFrame(...)` logic unless testing proves a manual override is the only stable fix.
- If a town-profile helper or equivalent vanilla-backed profile object is available in tModLoader, prefer that over bespoke profile plumbing, as long as it still allows Steroid Guide to keep its own head slot.
- No new custom body PNG should be introduced unless the Generator can demonstrate that tModLoader cannot keep the vanilla Guide body stable through the existing texture/profile APIs. The default plan is to reuse vanilla assets, not copy them into the mod.
- Verify that the existing `PreAI()` freeze behavior while the analyzer UI is open still works after the rendering changes, since the issue scope is visual and must not regress NPC interaction flow.

## UI/UX
- The visual result should be simple: the Steroid Guide should look like the vanilla Guide body at all times instead of briefly becoming garbled during motion.
- The housing/map/chat head icon should remain the Steroid Guide head asset already shipped in the mod.

## Success Criteria
- [ ] A spawned Steroid Guide renders correctly while idle and while walking across the world, with no sliced, stretched, or otherwise corrupted body frames.
- [ ] Facing-direction changes and other normal Guide-style animation transitions do not introduce texture corruption.
- [ ] Talking to the NPC and opening the analyzer still works exactly as before, and the NPC head icon remains the custom `SteroidGuideNPC_Head` asset.
- [ ] The implementation does not add a hardcoded custom body spritesheet unless a vanilla-texture approach is proven impossible during build work.

## Out of Scope
- Creating a brand-new custom full-body sprite for Steroid Guide
- Changing NPC stats, spawn rules, dialogue writing, analyzer UI, or recipe-analysis logic
- Adding shimmer-, party-, or biome-specific visual variants beyond what is needed to stop the current animation corruption
