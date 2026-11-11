using System.Collections.Generic;
using System.Globalization;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// Pools whose creatures came out in a pocket of navmesh joined to nothing, that the spawner
    /// does not take them out of by itself (Pools_off_pockets).
    ///
    /// A pool with an area is spread from the open ground nearest its point when the point is
    /// over a pocket (SpawnPoolManager.SpreadCentre). A pool without one is a point: one creature,
    /// put where the data has it, and a point is not moved by the spawner - a mission's actor is
    /// checked against it to the half metre. Each of these stood on a region of under 1,000 m²
    /// (NavMeshManager.UnstickPocketArea) joined to nothing, with the open ground of its map a
    /// few metres off: it could not come off it to chase anyone who shot it.
    ///  - 520001 Thraxus Machina Super Soldier, in Dybukkar, on 14 m² of catwalk beside the
    ///    sleep pods; the catwalk joined to the rest of the instance, 2.8 m off.
    ///  - 520007 Thrax Overseer Exing, Ashen Desert, on a 917 m² mound; the desert, 7.6 m off.
    ///  - 520062 Overseer Prysiam, in the Thrax camp by the Pools' Obelisk where
    ///    Move_bosses_off_safe_ground put him, on 161 m² inside its walls; the camp's floor, 5.1 m.
    ///  - 567230 Class V Stalker, 567231 Class V Predator and 567235 Sentinel, Devil's Den, each
    ///    against a Bane shard spike on 34 to 80 m²; the den's floor, 2 to 6 m off.
    /// Each is moved to that ground. So are five of the Warnet Caverns' wave spots (Kill the Queen:
    /// the lure's attack and the Queen's death, a warnet each sent at the nearest player of the
    /// squad), whose points are on 3 to 197 m² cut off from the Nest's floor: 571070, 571072, 571073 (the lure's), 571089 and 571093 (her death's), 3 to 6 m onto
    /// the Nest's floor. The spawner looks no further than a spot's own 2 m (SpawnPoolManager
    /// .PlacedSpotRadius), and found nothing there; three more of them it now takes down to the
    /// floor under them by itself.
    ///
    /// Two of the open-country packs (RegionSpawnpoolPreloader) stand at their region marker's
    /// height, and their ground is the first in the column under it:
    ///  - 540111 Flaregashers, Torden Plains: the marker is the top of the Brann viro-relay tower,
    ///    146 m over the canyon floor, and the tower's top is walkable - 632 m² of it, which the
    ///    pack never left. Its sisters at the same height (540113, 540114) stand on the canyon
    ///    floor. Moved to the floor at the tower's foot, as they are; the AFS turrets of the post
    ///    there are 40 m and more off, so the pack's area keeps 25 m from them, past their 18 m
    ///    scan (SpawnPoolManager.TurretScan).
    ///  - 540090 Beam Mantas, Torden Mires: on 34 m² of a rock vista off the Iapyx plateau. Under
    ///    it is the world's floor, 260 m down under the cloud layer; the plateau is 63 m off,
    ///    where its sister pack 540089 is, and the nearest of it clear of that pack and of the
    ///    control point 129 m. Made scripted (mode 2), as Spawn_pools_off_safe_ground did those it
    ///    would not move blind: it is not put down, until it is placed by hand.
    ///
    /// Not here, for want of telling what their ground should be from the mesh and the props: the
    /// Stalker Overlord (520073, Torden Plains), whose 569 m² is the ground under a bridge and the
    /// open ground 3.5 m over it may be the bridge's deck; the Ojasa Taskmaster (565204), on 75 m²
    /// over the hive's floor, which is 40 m off; two of Pravus Research's spots (650226, 650244),
    /// on 417 and 511 m² that may be rooms; the Kardash Colony's Lightbender Infiltrators
    /// (781223-781226, 781231-781234), in Brann corridors of 518 and 697 m² that the players walk
    /// too; and four of the Energy Weapon Center's spots (710102, 710201-710203), on 160 to 227 m²
    /// of its tunnels - the Nitroglazers among them in the reactor tubes they keep cool.
    ///
    /// Plain SQL, valid on SQLite and MySQL alike. A row moves only from where it was, and Down
    /// moves it back only from where Up put it, so a pool a GM has moved is left where they put it.
    /// </summary>
    public static class PoolsOffPockets
    {
        public sealed record Move(uint Id, uint MapContextId, string Name,
            double WasX, double WasY, double WasZ, double X, double Y, double Z);

        public static readonly Move[] Moves =
        {
            new(520001, 2190, "Thraxus Machina Super Soldier", 52.0, 331.0, -296.0, 52.4, 331.2, -298.81),
            new(520007, 1734, "Thrax Overseer Exing", -281.0, 340.4, -252.0, -285.53, 337.19, -257.21),
            new(520062, 1304, "Overseer Prysiam", -837.2, 791.2, -14.3, -842.0, 791.6, -16.0),
            new(567230, 1394, "Class V Stalker", 200.0, 137.68, 500.0, 200.31, 138.78, 494.62),
            new(567231, 1394, "Class V Predator", 175.55, 141.62, 530.83, 170.0, 142.22, 528.8),
            new(567235, 1394, "Sentinel", 230.0, 137.82, 520.0, 229.51, 138.07, 521.94),
            new(571070, 1384, "Warnet Caverns lure wave 1", 226.0, 6.62, 279.0, 222.89, 6.77, 279.94),
            new(571072, 1384, "Warnet Caverns lure wave 3", 238.0, 6.82, 291.0, 233.27, 6.39, 291.47),
            new(571073, 1384, "Warnet Caverns lure wave 4", 274.0, 3.03, 291.0, 273.08, 2.33, 293.92),
            new(571089, 1384, "Warnet Caverns death wave 8", 250.0, 3.09, 351.0, 249.8, 4.75, 347.8),
            new(571093, 1384, "Warnet Caverns death wave 12", 298.0, 7.67, 375.0, 299.2, 2.35, 376.8),
            new(540111, 1764, "Flaregashers", 897.8, 574.6, -138.1, 895.2, 428.2, -138.1),
        };

        /// <summary>The Beam Mantas on the rock vista off the Iapyx plateau.</summary>
        public const uint Retired = 540090;

        public const uint RetiredMap = 1759;

        public const short Automatic = 0;

        public const short Scripted = 2;

        public static IEnumerable<string> Up()
        {
            foreach (var m in Moves)
                yield return Place(m, m.WasX, m.WasY, m.WasZ, m.X, m.Y, m.Z);

            yield return Mode(Automatic, Scripted);
        }

        public static IEnumerable<string> Down()
        {
            foreach (var m in Moves)
                yield return Place(m, m.X, m.Y, m.Z, m.WasX, m.WasY, m.WasZ);

            yield return Mode(Scripted, Automatic);
        }

        private static string Place(Move m, double fromX, double fromY, double fromZ, double toX, double toY, double toZ) =>
            $"UPDATE {SpawnPoolEntry.TableName} SET pos_x = {N(toX)}, pos_y = {N(toY)}, pos_z = {N(toZ)} " +
            $"WHERE id = {m.Id} AND map_context_id = {m.MapContextId} " +
            $"AND ABS(pos_x - {N(fromX)}) < 0.01 AND ABS(pos_y - {N(fromY)}) < 0.01 AND ABS(pos_z - {N(fromZ)}) < 0.01;";

        private static string Mode(short from, short to) =>
            $"UPDATE {SpawnPoolEntry.TableName} SET mode = {to} WHERE id = {Retired} AND map_context_id = {RetiredMap} AND mode = {from};";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
