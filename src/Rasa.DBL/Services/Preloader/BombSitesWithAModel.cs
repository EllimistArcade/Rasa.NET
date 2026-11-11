using System.Collections.Generic;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The bomb sites a detonator is set on, at the Crater Lake Research Facility's two
    /// generators and Devil's Den's generator, detention center door and fuel depot
    /// (Bomb_sites_with_a_model). They were UsableBombForeanV01 (10122), whose class has no model
    /// of its own: mesh 776, the placeholder every inventory item has, and the client's catalog for
    /// the class lists default.geo and nothing else. Nothing showed where the bomb was, and there
    /// was nothing to click to set a detonator on it. They are UsableBombHumV01 (7870) now, the
    /// AFS charge's acceptor (prop_hum_bomb_acceptor_v01, with its arm and windup FX), the bomb
    /// site the Energy Weapon Center, Minos Caverns and Timora Mines already use.
    ///
    /// Crater Lake's are put down by the server (CraterLakeFacility) and take the class from
    /// CraterLakeContent.BombClass; Devil's Den's are world_usable rows, changed here. Plain SQL
    /// for both providers. Guarded: a row that is not the class this found is left as it is, both
    /// ways.
    /// </summary>
    public static class BombSitesWithAModel
    {
        public const uint BombClass = 7870;             // UsableBombHumV01
        public const uint ForeanBombClass = 10122;      // UsableBombForeanV01: mesh 776, no model

        /// <summary>Devil's Den's bomb sites: at the generator, the detention center's laser door, the fuel depot.</summary>
        public static readonly uint[] DevilsDenBombs = { DevilsDenContent.GeneratorBomb, DevilsDenContent.DoorBomb, DevilsDenContent.DepotBomb };

        private static string Ids => string.Join(", ", DevilsDenBombs);

        public static IEnumerable<string> Up()
        {
            yield return $"UPDATE world_usable SET class_id = {BombClass} WHERE id IN ({Ids}) AND class_id = {ForeanBombClass};";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"UPDATE world_usable SET class_id = {ForeanBombClass} WHERE id IN ({Ids}) AND class_id = {BombClass};";
        }
    }
}
