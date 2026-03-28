using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using SteroidGuide.Common.UI;
using SteroidGuide.Content.Players;

namespace SteroidGuide.Content.NPCs
{
    public class SteroidGuideProfile : ITownNPCProfile
    {
        public int RollVariation() => 0;

        public string GetNameForVariant(NPC npc) => npc.getNewNPCName();

        public Asset<Texture2D> GetTextureNPCShouldUse(NPC npc)
        {
            return TextureAssets.Npc[NPCID.Guide];
        }

        public int GetHeadTextureIndex(NPC npc)
        {
            return ModContent.GetModHeadSlot("SteroidGuide/Content/NPCs/SteroidGuideNPC_Head");
        }
    }

    [AutoloadHead]
    public class SteroidGuideNPC : ModNPC
    {
        public override string Texture => $"Terraria/Images/NPC_{NPCID.Guide}";

        public override string HeadTexture => "SteroidGuide/Content/NPCs/SteroidGuideNPC_Head";

        public override ITownNPCProfile TownNPCProfile() => new SteroidGuideProfile();

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 25;
            NPCID.Sets.ExtraFramesCount[Type] = 9;
            NPCID.Sets.AttackFrameCount[Type] = 4;
            NPCID.Sets.HatOffsetY[Type] = 4;

            NPC.Happiness
                .SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
                .SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
                .SetNPCAffection(NPCID.ArmsDealer, AffectionLevel.Dislike);

            NPCID.Sets.NPCBestiaryDrawModifiers value = new()
            {
                Velocity = 1f,
                Direction = -1
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
        }

        public override void SetDefaults()
        {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = NPCAIStyleID.Passive;
            NPC.damage = 0;
            NPC.defense = 15;
            NPC.lifeMax = 250;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.5f;
            AnimationType = NPCID.Guide;
        }

        public override bool PreAI()
        {
            var uiSystem = ModContent.GetInstance<RecipeAnalyzerUISystem>();
            if (uiSystem != null && uiSystem.IsVisible && uiSystem.TalkingNpcIndex == NPC.whoAmI)
            {
                NPC.velocity = Microsoft.Xna.Framework.Vector2.Zero;
                return false;
            }
            return true;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                new FlavorTextBestiaryInfoElement(
                    "A guide who has transcended ordinary recipe knowledge. " +
                    "He analyzes your entire inventory to reveal the most powerful items you can craft.")
            });
        }

        public override bool CanTownNPCSpawn(int numTownNPCs)
        {
            return true;
        }

        public override List<string> SetNPCNameList()
        {
            return new List<string>
            {
                "Arnold",
                "Chad",
                "Magnus",
                "Flex",
                "Atlas",
                "Thor",
                "Rex",
                "Victor"
            };
        }

        public override string GetChat()
        {
            var chat = new WeightedRandom<string>();

            chat.Add("I've memorized every recipe in existence. Try me.");
            chat.Add("The Guide shows you one recipe at a time? That's cute.");
            chat.Add("Bring your materials, open your chests, and I'll tell you what you can really make.");
            chat.Add("I can see the full picture. Every crafting chain, every possibility.");

            int guideIndex = NPC.FindFirstNPC(NPCID.Guide);
            if (guideIndex >= 0)
            {
                string guideName = Main.npc[guideIndex].GivenName;
                chat.Add($"Don't tell {guideName} I said this, but his recipe book is... incomplete.");
            }

            if (Main.bloodMoon)
                chat.Add("Even during a Blood Moon, the recipes don't change. Focus.");

            if (Main.raining)
                chat.Add("Rain doesn't stop the forge. Let's see what you can craft.");

            if (Main.hardMode)
                chat.Add("New ores, new souls, new possibilities. Let me analyze your inventory again.");

            if (NPC.downedMoonlord)
                chat.Add("You've conquered the Moon Lord, but have you crafted the Zenith? Let me check.");

            var modPlayer = Main.LocalPlayer.GetModPlayer<SteroidGuideModPlayer>();
            if (modPlayer.RecentlyDied)
                chat.Add($"{Main.LocalPlayer.name} should have crafted better armor. I told them what was available.");

            return chat;
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = "Analyze Recipes";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
            {
                var uiSystem = ModContent.GetInstance<RecipeAnalyzerUISystem>();
                uiSystem?.ShowUI(NPC.whoAmI);

                Main.player[Main.myPlayer].SetTalkNPC(-1);
                Main.npcChatText = "";
            }
        }
    }
}
