using System.Collections.Generic;
using System.Globalization;

using Rasa.Structures.World;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The Eloh obelisks the 1.16.5 client's maps leave out under their Logos signs. At six places the client's
    /// .map has a stack of ArchElohLogosSign entities but nothing under it, so the signs float 7-8 m above the floor;
    /// the server had nothing there either.
    ///
    /// Sourced from the client's .map files (all 77 read: 348 Logos signs, about 115 stacks). A sign stack on an
    /// obelisk stands at the obelisk's x and z, its signs facing opposite the obelisk, the lowest 6.6-9.3 m above its
    /// base; every other stack hangs over an Eloh doorway, corridor or reliquary. The six here have nothing within
    /// 4 m, and their lowest sign is 7.2-8.1 m above the navmesh's floor.
    ///
    /// Reconstructed: an ArchElohObeliskV02 (client class 10260), the obelisk under the Divide's other sign stacks, at
    /// each stack's x and z, yaw the signs' minus pi. The base: on the Divide hilltop, which slopes (165.1 to 165.75
    /// round it), 8.4 m under the lower sign, as "Evil Cave", the Divide's other two-sign obelisk; elsewhere on the
    /// flat floor under the signs (the navmesh's, within 0.2 m over 3 m round each), the lowest sign 7.19-7.65 m above
    /// it, between the three-sign obelisks' 7.9 and the five-sign ones' 6.6. Public, as world_destructible rows that
    /// PlacedDestructibles puts down as scenery with no state, as PoolsObeliskV1.
    ///
    /// Not here: the six single signs over Trinity Bridge (the Array of Hope's conduits, PlateauArrayOfHopeV1) and
    /// the three "Earth" signs among the Wilderness's human base at (-140, -617).
    ///
    /// Ids: world_destructible 572001-572006, free before (572001 first came in as Divide_remember_obelisk, at
    /// 20271023000000, the timestamp Crater_lake_pen_generator has: <see cref="ForgetOldId"/>). Plain SQL, valid on
    /// SQLite and MySQL; each row goes in only where it is not, and down takes out only these rows.
    /// </summary>
    public static class MissingElohObelisks
    {
        public const uint ObeliskClass = 10260;         // ArchElohObeliskV02

        public sealed record Obelisk(uint Id, uint MapContextId, double X, double Y, double Z, double Rotation,
            double LowerSignY, double SignYaw, string Signs, string Comment);

        public static readonly Obelisk[] All =
        {
            new(572001, 1148, 667.1, 165.4, -419.2, 0, 173.8, 3.14159265, "We/Us, Remember",
                "Eloh obelisk under the We/Us and Remember signs, Concordia Divide hilltop"),
            new(572002, 2084, -68.00, 139.47, 585.75, -0.6036, 146.72, 2.53795, "War, True, Enemy, Give",
                "Eloh obelisk under the War, True, Enemy and Give signs, Eloh Communication Tower, west of the approach"),
            new(572003, 2084, -39.75, 139.47, 585.75, 0.6147, 146.72, -2.52694, "Yours, Knowledge, Planet, Now",
                "Eloh obelisk under the Yours, Knowledge, Planet and Now signs, Eloh Communication Tower, east of the approach"),
            new(572004, 2103, -208.50, 366.71, 444.25, -0.3803, 373.97, 2.76128, "Power, Awake, Planet, Honor",
                "Eloh obelisk under the Power, Awake, Planet and Honor signs, Fault Lever, Hidden Cave"),
            new(572005, 1773, -108.00, 192.03, 324.00, -1.5708, 199.22, 1.57080, "Door, Cosmos, Enemy, Attack",
                "Eloh obelisk under the Door, Cosmos, Enemy and Attack signs, Kardash Atta Colony, Eloh Cache"),
            new(572006, 1823, 352.90, 185.79, -283.45, -1.5708, 193.44, 1.57080, "Control, Through, Function, Neph",
                "Eloh obelisk under the Control, Through, Function and Neph signs, Sanctus Grotto"),
        };

        /// <summary>The id the Divide's obelisk first came in under, which Crater_lake_pen_generator shares.</summary>
        public const string OldId = "20271023000000_Divide_remember_obelisk";

        /// <summary>The history row a World that ran it under OldId still has.</summary>
        public static readonly string ForgetOldId =
            $"DELETE FROM __EFMigrationsHistory WHERE MigrationId = '{OldId}';";

        public static IEnumerable<string> Up()
        {
            foreach (var o in All)
                yield return "INSERT INTO world_destructible (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, hit_points, " +
                    "creature_id, creature_count, side, comment) " +
                    $"SELECT {o.Id}, {o.MapContextId}, {ObeliskClass}, {F(o.X)}, {F(o.Y)}, {F(o.Z)}, {F(o.Rotation)}, 0, 0, 0, 0, " +
                    $"'{o.Comment}' FROM {MapInfoEntry.TableName} m WHERE m.map_context_id = {o.MapContextId} " +
                    $"AND NOT EXISTS (SELECT 1 FROM world_destructible x WHERE x.id = {o.Id});";
        }

        public static IEnumerable<string> Down()
        {
            foreach (var o in All)
                yield return $"DELETE FROM world_destructible WHERE id = {o.Id};";
        }

        private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
