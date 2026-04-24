using System.IO;
using SteroidGuide.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide
{
    public class SteroidGuideMod : Mod
    {
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            var msgType = (MessageType)reader.ReadByte();
            switch (msgType)
            {
                case MessageType.RequestChestContents:
                {
                    if (Main.netMode != NetmodeID.Server)
                        break;
                    int chestIndex = reader.ReadInt32();
                    if (chestIndex < 0 || chestIndex >= Main.maxChests || Main.chest[chestIndex] == null)
                        break;
                    ChestSyncSystem.Enqueue(chestIndex, whoAmI);
                    break;
                }
                case MessageType.ChestContentsReady:
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        break;
                    int chestIndex = reader.ReadInt32();
                    // Order matters: snapshot first so any concurrent scan observes the new contents
                    // together with the new timestamp, then flip the sync timestamp.
                    ItemScanner.UpdateChestContentsFromMainChest(chestIndex);
                    ItemScanner.MarkChestSynced(chestIndex);
                    break;
                }
            }
        }
    }
}
