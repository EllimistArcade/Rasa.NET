namespace Rasa.Data
{
    public enum LootQuality
    {
        Mission = 1,
        Normal = 2,
        Uncommon = 3,
        Rare = 4,
        Epic = 5,
        Legendary = 6,
        Junk = 7
    }

    public static class LootQualityExtensions
    {
        /// <summary>
        /// How good this quality is, low to high.
        ///
        /// **The enum values are not an order.** They are the client's own ids
        /// (generated.client.quality), where Junk is 7 - the largest number and the least
        /// valuable thing there is. Comparing the ids directly gets auto-loot exactly backwards:
        /// a Junk threshold would take everything except junk. The client's own option list is
        /// ordered Junk, Normal, Uncommon, Rare, Epic, which is the order below.
        ///
        /// Mission items sit outside it. They are quest pickups rather than loot of a quality,
        /// and are ranked above everything so no threshold sweeps them up by accident.
        /// </summary>
        public static int Rank(this LootQuality quality)
        {
            return quality switch
            {
                LootQuality.Junk => 0,
                LootQuality.Normal => 1,
                LootQuality.Uncommon => 2,
                LootQuality.Rare => 3,
                LootQuality.Epic => 4,
                LootQuality.Legendary => 5,
                LootQuality.Mission => int.MaxValue,
                _ => int.MaxValue
            };
        }
    }
}
