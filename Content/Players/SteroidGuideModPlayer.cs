using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace SteroidGuide.Content.Players
{
    public class SteroidGuideModPlayer : ModPlayer
    {
        public bool RecentlyDied;
        private int _deathTimer;
        private const int DeathDialogueDuration = 3600; // 60 seconds at 60fps

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            RecentlyDied = true;
            _deathTimer = DeathDialogueDuration;
        }

        public override void PostUpdate()
        {
            if (RecentlyDied)
            {
                _deathTimer--;
                if (_deathTimer <= 0)
                    RecentlyDied = false;
            }
        }
    }
}
