using System.Collections.Generic;

namespace Rasa.Services.Preloader.Missions.Marshes
{
    /// <summary>
    /// Conscript Kaav, the Bane Traitor of The Regulator (MarshesRegulatorNpcsV1.Kaav), back five minutes after he is killed,
    /// not two seconds (Marshes_kaav_respawn).
    ///
    /// He is NEUTRAL, as the Pools' Nakrinx he is made from, so that the Depot's Bane leave him alone; NEUTRAL is a side any
    /// player may attack (TargetCategories.PlayerMayAttack), and his pool (respown_time 20) put him back at once: a level 37
    /// kill with a conversation, on tap, as Vogren's hologram was (Vogren_hologram_friendly). Five minutes, as every other
    /// NPC a player may kill and talk to.
    ///
    /// Plain SQL, valid on SQLite and MySQL. Up changes the pool only from two seconds, Down only from five minutes.
    /// </summary>
    public static class MarshesKaavRespawnV1
    {
        public const uint Pool = MarshesRegulatorNpcsV1.Kaav;
        public const uint WasTenths = 20, NowTenths = 3000;

        public static IEnumerable<string> Up()
        {
            yield return Respawn(WasTenths, NowTenths);
        }

        public static IEnumerable<string> Down()
        {
            yield return Respawn(NowTenths, WasTenths);
        }

        private static string Respawn(uint from, uint to) =>
            $"UPDATE spawnpool SET respown_time = {to} WHERE id = {Pool} AND respown_time = {from};";
    }
}
