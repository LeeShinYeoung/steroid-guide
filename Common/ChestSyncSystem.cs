using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SteroidGuide.Common
{
    public class ChestSyncSystem : ModSystem
    {
        private struct PendingChestSync
        {
            public int ChestIndex;
            public int ToClient;
            public int NextSlot;
        }

        private static readonly Queue<PendingChestSync> _queue = new();
        private const int MaxPacketsPerFrame = 80;

        public static void Enqueue(int chestIndex, int toClient)
        {
            _queue.Enqueue(new PendingChestSync
            {
                ChestIndex = chestIndex,
                ToClient = toClient,
                NextSlot = 0
            });
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode != NetmodeID.Server || _queue.Count == 0)
                return;

            int sent = 0;
            while (_queue.Count > 0 && sent < MaxPacketsPerFrame)
            {
                var pending = _queue.Dequeue();

                if (Main.chest[pending.ChestIndex] == null)
                    continue;

                int slotsToSend = System.Math.Min(
                    Chest.maxItems - pending.NextSlot,
                    MaxPacketsPerFrame - sent);

                for (int i = 0; i < slotsToSend; i++)
                {
                    NetMessage.SendData(MessageID.SyncChestItem,
                        pending.ToClient, -1, null,
                        pending.ChestIndex, pending.NextSlot + i);
                    sent++;
                }

                pending.NextSlot += slotsToSend;

                if (pending.NextSlot < Chest.maxItems)
                {
                    _queue.Enqueue(pending);
                }
                else
                {
                    var mod = ModContent.GetInstance<SteroidGuideMod>();
                    var reply = mod.GetPacket();
                    reply.Write((byte)MessageType.ChestContentsReady);
                    reply.Write(pending.ChestIndex);
                    reply.Send(pending.ToClient);
                }
            }
        }

        public override void OnWorldUnload()
        {
            _queue.Clear();
        }
    }
}
