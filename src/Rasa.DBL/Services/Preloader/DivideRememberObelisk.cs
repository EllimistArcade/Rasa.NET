using System.Collections.Generic;
using System.Globalization;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The Eloh obelisk under the "We/Us" and "Remember" Logos signs on the hilltop at (667, -419) in Concordia Divide
    /// (map 1148). The 1.16.5 client's map places the two signs (ArchElohLogosSignWe_or_Us at y 174.7,
    /// ArchElohLogosSignRemember at y 173.8) but no obelisk under them, so they float 8 m above the hill; the server
    /// had nothing there either.
    ///
    /// Sourced from the same .map: each of the Divide's other three sign-topped obelisks is an ArchElohObeliskV02
    /// (client class 10260) at the signs' x and z, facing opposite them. "Evil Cave" at (640.7, 93.6), the other
    /// two-sign one, has its base 8.4 m under the lower sign; the two "Place of Honor" obelisks, with three signs,
    /// 7.9 m.
    ///
    /// Reconstructed: ArchElohObeliskV02 at the signs' (667.1, -419.2), base 8.4 m under the lower sign (y 165.4; the
    /// navmesh has the hilltop at 165.1 to 165.75 there), yaw 0 (the signs face pi). Public, as a world_destructible
    /// row that PlacedDestructibles puts down as scenery with no state, as PoolsObeliskV1.
    ///
    /// Id: world_destructible 572001, free before. Plain SQL, valid on SQLite and MySQL; down takes out only this row.
    /// </summary>
    public static class DivideRememberObelisk
    {
        public const uint Id = 572001;
        public const uint MapContextId = 1148;          // adv_foreas_concordia_divide
        public const uint ObeliskClass = 10260;         // ArchElohObeliskV02
        public const double Rotation = 0;
        public static readonly (double X, double Y, double Z) At = (667.1, 165.4, -419.2);

        /// <summary>The lower of the two signs the client's map puts there (ArchElohLogosSignRemember).</summary>
        public static readonly (double X, double Y, double Z) LowerSign = (667.1, 173.8, -419.2);

        public static IEnumerable<string> Up()
        {
            yield return "INSERT INTO world_destructible (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, hit_points, " +
                "creature_id, creature_count, side, comment) " +
                $"VALUES ({Id}, {MapContextId}, {ObeliskClass}, {F(At.X)}, {F(At.Y)}, {F(At.Z)}, {F(Rotation)}, 0, 0, 0, 0, " +
                "'Eloh obelisk under the We/Us and Remember signs, Concordia Divide hilltop');";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"DELETE FROM world_destructible WHERE id = {Id};";
        }

        private static string F(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
