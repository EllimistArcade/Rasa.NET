using System.Collections.Generic;

namespace Rasa.Services.Preloader.Missions.Plateau
{
    /// <summary>
    /// Vogren's hologram, the Holographic Projection Device at Vogren's Tomb (PlateauVogrenNpcsV1.Hologram), FRIENDLY
    /// again (Vogren_hologram_friendly).
    ///
    /// Plateau_vogren_npcs made it NEUTRAL so that the Tomb Miasmas would leave it alone (to-fix #39). NEUTRAL is a
    /// side anyone may attack (TargetCategories.PlayerMayAttack): players killed it with a hit or two, at level 34 and
    /// 1,000 hit points, and its pool (respown_time 20, two seconds) put it back at once - experience without end, two
    /// levels in ten minutes from 28. FRIENDLY, as every other giver and receiver, no player may attack it; the game
    /// server keeps it out of every creature's fight, its own and the miasmas' (NpcsKeptFromCreatures).
    ///
    /// Plain SQL, valid on SQLite and MySQL. Up changes it only from NEUTRAL, Down only from FRIENDLY.
    /// </summary>
    public static class PlateauVogrenHologramFriendlyV1
    {
        public const uint Friendly = 1, Neutral = 3;

        public static IEnumerable<string> Up()
        {
            yield return Faction(Neutral, Friendly);
        }

        public static IEnumerable<string> Down()
        {
            yield return Faction(Friendly, Neutral);
        }

        private static string Faction(uint from, uint to) =>
            $"UPDATE creature SET faction = {to} WHERE id = {PlateauVogrenNpcsV1.Hologram} AND faction = {from};";
    }
}
