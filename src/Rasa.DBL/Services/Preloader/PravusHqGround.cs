using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Two of the Thrax packs in the compound about Pravus Research's HQ on the ground (Pravus_hq_ground). Their
    /// pools stood on a flat plane at 33 the navmesh has under the hollows there, 2.5 and 5 m below the ground: an
    /// island of its own, over 2000 m2 and so too large for the server to take a pack off it
    /// (NavMeshManager.UnstickPocketArea), where the Thrax spawned and stayed out of reach under the ground. Each
    /// stands on the ground over its spot now, as PravusResearchContent places it.
    ///
    /// Guarded: a pool not at its spot and height as this found it is left as it is, both ways.
    /// </summary>
    public static class PravusHqGround
    {
        public const uint Map = PravusResearchContent.Pravus;

        /// <summary>Each pool, its spot, and its height on the plane under the ground.</summary>
        public static readonly (uint Pool, double X, double Z, double WasY)[] Under =
        {
            (650232, 150, 30, 32.94),
            (650233, 150, -50, 33.14)
        };

        /// <summary>How far from a height a pool may be and still be taken for at it.</summary>
        private const double Near = 0.05;

        /// <summary>Its height on the ground, as PravusResearchContent places it.</summary>
        public static double NowY(uint pool) => PravusResearchContent.Pools.Single(p => p.Id == pool).Y;

        public static IEnumerable<string> Up() =>
            Under.Select(p => Move(p.Pool, p.X, p.Z, p.WasY, NowY(p.Pool)));

        public static IEnumerable<string> Down() =>
            Under.Reverse().Select(p => Move(p.Pool, p.X, p.Z, NowY(p.Pool), p.WasY));

        private static string Move(uint pool, double x, double z, double from, double to) =>
            $"UPDATE spawnpool SET pos_y = {N(to)} WHERE id = {pool} AND map_context_id = {Map} AND pos_x = {N(x)} AND pos_z = {N(z)} " +
            $"AND pos_y > {N(from - Near)} AND pos_y < {N(from + Near)};";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
