using Terraria;
using Terraria.ModLoader;
using SteroidGuide.Content.NPCs;

namespace SteroidGuide.Content.World
{
    public class SteroidGuideWorldGen : ModSystem
    {
        public override void PostWorldGen()
        {
            int npcType = ModContent.NPCType<SteroidGuideNPC>();
            int spawnX = Main.spawnTileX * 16;
            int spawnY = Main.spawnTileY * 16;
            NPC.NewNPC(NPC.GetSource_NaturalSpawn(), spawnX, spawnY, npcType);
        }
    }
}
