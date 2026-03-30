using Terraria;

namespace SteroidGuide.Common
{
    public enum FilterCategory
    {
        All,
        Weapons,
        Armor,
        Accessories,
        Tools,
        Consumables,
        Placeables,
        Materials,
        Misc
    }

    public static class ItemCategoryClassifier
    {
        public static FilterCategory Classify(int itemId)
        {
            var item = new Item();
            item.SetDefaults(itemId);
            return Classify(item);
        }

        public static FilterCategory Classify(Item item)
        {
            if (item == null)
            {
                return FilterCategory.Misc;
            }

            if (IsArmor(item))
            {
                return FilterCategory.Armor;
            }

            if (item.accessory)
            {
                return FilterCategory.Accessories;
            }

            if (IsTool(item))
            {
                return FilterCategory.Tools;
            }

            if (item.damage > 0 || item.ammo > 0)
            {
                return FilterCategory.Weapons;
            }

            if (item.createTile != -1 || item.createWall != -1)
            {
                return FilterCategory.Placeables;
            }

            if (item.potion || item.buffType > 0 || item.healLife > 0 || item.healMana > 0 || item.consumable)
            {
                return FilterCategory.Consumables;
            }

            if (item.material)
            {
                return FilterCategory.Materials;
            }

            return FilterCategory.Misc;
        }

        private static bool IsArmor(Item item)
        {
            return item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0;
        }

        private static bool IsTool(Item item)
        {
            return item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0;
        }
    }
}
