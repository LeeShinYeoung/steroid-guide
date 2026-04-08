using System.Collections.Generic;
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
    [AutoloadHead]
    public class SteroidGuideNPC : ModNPC
    {
        private const int VanillaGuideType = NPCID.Guide;
        private static Profiles.StackedNPCProfile NPCProfile;

        public override ITownNPCProfile TownNPCProfile()
        {
            return NPCProfile;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Main.npcFrameCount[VanillaGuideType];
            NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[VanillaGuideType];
            NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[VanillaGuideType];
            NPCID.Sets.HatOffsetY[Type] = NPCID.Sets.HatOffsetY[VanillaGuideType];
            NPCID.Sets.DangerDetectRange[Type] = 700;
            NPCID.Sets.AttackType[Type] = 0;
            NPCID.Sets.AttackTime[Type] = 90;
            NPCID.Sets.AttackAverageChance[Type] = 30;

            NPCProfile = new Profiles.StackedNPCProfile(
                new Profiles.DefaultNPCProfile(Texture, NPCHeadLoader.GetHeadSlot(HeadTexture))
            );

            NPC.Happiness
                .SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
                .SetBiomeAffection<OceanBiome>(AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Clothier, AffectionLevel.Like)
                .SetNPCAffection(NPCID.BestiaryGirl, AffectionLevel.Like)
                .SetNPCAffection(NPCID.Steampunker, AffectionLevel.Dislike)
                .SetNPCAffection(NPCID.Painter, AffectionLevel.Hate);

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
            AnimationType = VanillaGuideType;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                new FlavorTextBestiaryInfoElement(this.GetLocalizedValue("Bestiary"))
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

            chat.Add(this.GetLocalizedValue("Chat.Standard1"));
            chat.Add(this.GetLocalizedValue("Chat.Standard2"));
            chat.Add(this.GetLocalizedValue("Chat.Standard3"));
            chat.Add(this.GetLocalizedValue("Chat.Standard4"));

            int guideIndex = NPC.FindFirstNPC(NPCID.Guide);
            if (guideIndex >= 0)
            {
                string guideName = Main.npc[guideIndex].GivenName;
                chat.Add(string.Format(this.GetLocalizedValue("Chat.GuidePresent"), guideName));
            }

            if (Main.bloodMoon)
                chat.Add(this.GetLocalizedValue("Chat.BloodMoon"));

            if (Main.raining)
                chat.Add(this.GetLocalizedValue("Chat.Rain"));

            if (Main.hardMode)
                chat.Add(this.GetLocalizedValue("Chat.HardMode"));

            if (NPC.downedMoonlord)
                chat.Add(this.GetLocalizedValue("Chat.MoonLord"));

            var modPlayer = Main.LocalPlayer.GetModPlayer<SteroidGuideModPlayer>();
            if (modPlayer.RecentlyDied)
                chat.Add(string.Format(this.GetLocalizedValue("Chat.PlayerDied"), Main.LocalPlayer.name));

            return chat;
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = this.GetLocalizedValue("CraftableButton");
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton)
            {
                var uiSystem = ModContent.GetInstance<CraftableUISystem>();
                uiSystem?.ShowUI(NPC.whoAmI);
            }
        }
    }
}
