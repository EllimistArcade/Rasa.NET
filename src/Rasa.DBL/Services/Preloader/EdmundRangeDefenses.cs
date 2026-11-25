using System.Collections.Generic;
using System.Globalization;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// Edmund Range's control points' turrets and force fields (Edmund_range_defenses), as the
    /// range's design had them: "The Central CP has 4 Turrets. The East and West have 6 turrets.
    /// Every CP has 4 Force Fields and an entry point on each side", the turrets and fields the
    /// holder's, and the Bane's while they hold it.
    ///
    /// The map builds them in. Whiskey (west) and Echo (east) are each fenced in with four
    /// ArchHumOutpostFenceCornerV03 and two ArchHumOutpostFenceStraight8V03, six round gun bases
    /// in all; Charlie (centre) with four ArchHumOutpostFenceCornerV12, four bases. Each point has
    /// an ArchHumOutpostFenceGateV02 in the middle of each side. Positions and facings are the
    /// pieces' own, from the 1.16.5.0 client's adv_wargame_edmundrange2.map:
    ///  - a base is the piece's Human_base_turret bone: (-15.988, 6.241, -7.888) from a corner's
    ///    origin, (-3.993, 6.256, -7.895) from a Straight8V03's (Fence_turret_bases, Afs_turret_bases).
    ///    CornerV12's is taken as CornerV03's: the range's navmesh has a round floor 6.24 m up at
    ///    each of the four, as it has at the V03s'. The two depots' fences (north of Whiskey,
    ///    south of Echo) and their bases are no point's, and are left empty.
    ///  - a field stands in its gate's nooks, at the gate's local (8, 0, -1.4567) facing the
    ///    gate's way (Gate_field_nooks).
    ///
    /// On each base, two spawn pools of one turret, as Add_control_point_turrets has its spots:
    ///  - the holder's (control_point_link kind 2): Howling Maw's AFS standard turret (550019,
    ///    level 48, the level of the range's Simulated Bane), mode 1 - set down only while a team
    ///    holds the point, and fighting for that team (Battlegrounds) - back
    ///    <see cref="TurretRespawn"/> after it is destroyed, the design's three minutes;
    ///  - the Bane's (kind 1): Howling Maw's Bane turret (551014) 0.38 m over the base
    ///    (Afs_turret_bases), one of the point's Simulated Bane, which must all be dead before the
    ///    point can be taken and are not back until the next match.
    /// Both face out from the point.
    ///
    /// Each field is a world_destructible row of the ownable force field (10000083, the outpost
    /// gate's, hued) linked to its point (kind 8): the Bane's side on the row, which the match
    /// makes nobody's team field, and the holder's when a team takes the point (ForceFields.SetTeam).
    ///
    /// Plain SQL, valid on SQLite and MySQL alike. A row goes in only where its id is free, a
    /// link only to a row that is there; Down takes out the id ranges and their links.
    /// </summary>
    public static class EdmundRangeDefenses
    {
        public const uint MapContextId = EdmundRangeSeed.MapContextId;

        /// <summary>The holder's turret, Emplacement_AFS_Turret_Standard of Howling Maw (ControlPointTurrets), and the Bane's.</summary>
        public const uint TeamTurret = 550019, BaneTurret = 551014;

        /// <summary>A turret's spawn pool respawn time, in tenths of a second: three minutes.</summary>
        public const uint TurretRespawn = 1800;

        /// <summary>The Bane turrets' pools respawn as the Simulated Bane's do (EdmundRangeSeed): only when the field is reset.</summary>
        public const uint BaneRespawn = 600;

        /// <summary>How far over the base the Bane turret stands (Afs_turret_bases).</summary>
        public const double BaneOverBase = 0.38;

        /// <summary>The ownable force field (UsableOwnableForceFieldHumOutpostV01, OWNABLEFORCEFIELD).</summary>
        public const uint FieldClass = 10000083;

        public const uint FirstTeamPool = 595201, FirstBanePool = 595221, FirstField = 595301;

        public sealed record Spot(uint Point, double X, double Y, double Z, double Rotation, string Where);

        public sealed record Gate(uint Point, double X, double Y, double Z, double Rotation, string Where);

        /// <summary>The bases, a point's together, each turret facing out from its point.</summary>
        public static readonly Spot[] Spots =
        {
            new(EdmundRangeSeed.WhiskeyId, -328.988, 427.241, -142.888, 0.7094, "CornerV03 at (-313, 421, -135), yaw 0"),
            new(EdmundRangeSeed.WhiskeyId, -249.112, 427.241, -142.988, -0.6815, "CornerV03 at (-257, 421, -127), yaw -90"),
            new(EdmundRangeSeed.WhiskeyId, -249.105, 427.256, -106.993, -1.2737, "Straight8V03 at (-257, 421, -103), yaw -90"),
            new(EdmundRangeSeed.WhiskeyId, -249.105, 427.256, -82.993, -1.8728, "Straight8V03 at (-257, 421, -79), yaw -90"),
            new(EdmundRangeSeed.WhiskeyId, -249.012, 427.241, -47.112, -2.4598, "CornerV03 at (-265, 421, -55), yaw 180"),
            new(EdmundRangeSeed.WhiskeyId, -328.888, 427.241, -47.012, 2.4364, "CornerV03 at (-321, 421, -63), yaw 90"),
            new(EdmundRangeSeed.CharlieId, -99.988, 436.241, 0.112, 0.7867, "CornerV12 at (-84, 430, 8), yaw 0"),
            new(EdmundRangeSeed.CharlieId, -20.112, 436.241, 0.012, -0.7841, "CornerV12 at (-28, 430, 16), yaw -90"),
            new(EdmundRangeSeed.CharlieId, -20.012, 436.241, 79.888, -2.3549, "CornerV12 at (-36, 430, 72), yaw 180"),
            new(EdmundRangeSeed.CharlieId, -99.888, 436.241, 79.988, 2.3574, "CornerV12 at (-92, 430, 64), yaw 90"),
            new(EdmundRangeSeed.EchoId, 130.105, 427.256, 162.993, 1.2785, "Straight8V03 at (138, 421, 159), yaw 90"),
            new(EdmundRangeSeed.EchoId, 130.012, 427.241, 127.112, 0.6957, "CornerV03 at (146, 421, 135), yaw 0"),
            new(EdmundRangeSeed.EchoId, 209.888, 427.241, 127.012, -0.6935, "CornerV03 at (202, 421, 143), yaw -90"),
            new(EdmundRangeSeed.EchoId, 209.988, 427.241, 222.888, -2.4459, "CornerV03 at (194, 421, 215), yaw 180"),
            new(EdmundRangeSeed.EchoId, 130.112, 427.241, 222.988, 2.4481, "CornerV03 at (138, 421, 207), yaw 90"),
            new(EdmundRangeSeed.EchoId, 130.105, 427.256, 186.993, 1.8628, "Straight8V03 at (138, 421, 183), yaw 90")
        };

        /// <summary>The gates' fields, a point's together: south, east, north and west of it.</summary>
        public static readonly Gate[] Gates =
        {
            new(EdmundRangeSeed.WhiskeyId, -289.0, 421.0, -136.4567, 0.0, "GateV02 at (-297, 421, -135)"),
            new(EdmundRangeSeed.WhiskeyId, -255.5433, 421.0, -95.0, -1.5708, "GateV02 at (-257, 421, -103)"),
            new(EdmundRangeSeed.WhiskeyId, -289.0, 421.0, -53.5433, 3.1416, "GateV02 at (-281, 421, -55)"),
            new(EdmundRangeSeed.WhiskeyId, -322.4567, 421.0, -95.0, 1.5708, "GateV02 at (-321, 421, -87)"),
            new(EdmundRangeSeed.CharlieId, -60.0, 430.0, 6.5433, 0.0, "GateV02 at (-68, 430, 8)"),
            new(EdmundRangeSeed.CharlieId, -26.5433, 430.0, 40.0, -1.5708, "GateV02 at (-28, 430, 32)"),
            new(EdmundRangeSeed.CharlieId, -60.0, 430.0, 73.4567, 3.1416, "GateV02 at (-52, 430, 72)"),
            new(EdmundRangeSeed.CharlieId, -93.4567, 430.0, 40.0, 1.5708, "GateV02 at (-92, 430, 48)"),
            new(EdmundRangeSeed.EchoId, 170.0, 421.0, 133.5433, 0.0, "GateV02 at (162, 421, 135)"),
            new(EdmundRangeSeed.EchoId, 203.4567, 421.0, 175.0, -1.5708, "GateV02 at (202, 421, 167)"),
            new(EdmundRangeSeed.EchoId, 170.0, 421.0, 216.4567, 3.1416, "GateV02 at (178, 421, 215)"),
            new(EdmundRangeSeed.EchoId, 136.5433, 421.0, 175.0, 1.5708, "GateV02 at (138, 421, 183)")
        };

        public static uint TeamPoolOf(int spot) => FirstTeamPool + (uint)spot;
        public static uint BanePoolOf(int spot) => FirstBanePool + (uint)spot;
        public static uint FieldOf(int gate) => FirstField + (uint)gate;

        public static uint LastTeamPool => TeamPoolOf(Spots.Length - 1);
        public static uint LastBanePool => BanePoolOf(Spots.Length - 1);
        public static uint LastField => FieldOf(Gates.Length - 1);

        public static IEnumerable<string> Up()
        {
            for (var i = 0; i < Spots.Length; i++)
            {
                var spot = Spots[i];

                foreach (var sql in Pool(spot, TeamPoolOf(i), TeamTurret, TurretRespawn, spot.Y, ControlPointLinkEntry.KindAfsPool))
                    yield return sql;
                foreach (var sql in Pool(spot, BanePoolOf(i), BaneTurret, BaneRespawn, spot.Y + BaneOverBase, ControlPointLinkEntry.KindBanePool))
                    yield return sql;
            }

            for (var i = 0; i < Gates.Length; i++)
            {
                var gate = Gates[i];
                var id = FieldOf(i);

                yield return $"INSERT INTO {WorldDestructibleEntry.TableName} (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, hit_points, creature_id, creature_count, side, comment) " +
                    $"SELECT {id}, {MapContextId}, {FieldClass}, {N(gate.X)}, {N(gate.Y)}, {N(gate.Z)}, {N(gate.Rotation)}, 0, 0, 0, {WorldDestructibleEntry.SideBane}, " +
                    $"'{PointName(gate.Point)} force field, {gate.Where}' " +
                    $"FROM entityclass e WHERE e.id = {FieldClass} AND EXISTS (SELECT 1 FROM {ControlPointEntry.TableName} p WHERE p.id = {gate.Point}) " +
                    $"AND NOT EXISTS (SELECT 1 FROM {WorldDestructibleEntry.TableName} x WHERE x.id = {id});";
                yield return Link(gate.Point, ControlPointLinkEntry.KindForceField, id,
                    $"SELECT 1 FROM {WorldDestructibleEntry.TableName} d WHERE d.id = {id} AND d.map_context_id = {MapContextId} AND d.class_id = {FieldClass}");
            }
        }

        public static IEnumerable<string> Down()
        {
            yield return $"DELETE FROM {ControlPointLinkEntry.TableName} WHERE control_point_id BETWEEN {EdmundRangeSeed.WhiskeyId} AND {EdmundRangeSeed.EchoId} " +
                $"AND ((kind = {ControlPointLinkEntry.KindAfsPool} AND object_id BETWEEN {FirstTeamPool} AND {LastTeamPool}) " +
                $"OR (kind = {ControlPointLinkEntry.KindBanePool} AND object_id BETWEEN {FirstBanePool} AND {LastBanePool}) " +
                $"OR (kind = {ControlPointLinkEntry.KindForceField} AND object_id BETWEEN {FirstField} AND {LastField}));";
            yield return $"DELETE FROM spawnpool WHERE map_context_id = {MapContextId} AND (id BETWEEN {FirstTeamPool} AND {LastTeamPool} OR id BETWEEN {FirstBanePool} AND {LastBanePool});";
            yield return $"DELETE FROM {WorldDestructibleEntry.TableName} WHERE map_context_id = {MapContextId} AND id BETWEEN {FirstField} AND {LastField};";
        }

        private static IEnumerable<string> Pool(Spot spot, uint id, uint creature, uint respawn, double y, byte kind)
        {
            yield return "INSERT INTO spawnpool (id, mode, anim_type, respown_time, pos_x, pos_y, pos_z, rotation, map_context_id, " +
                "creature_1_Id, creature_1_min_count, creature_1_max_count, creature_2_Id, creature_2_min_count, creature_2_max_count, " +
                "creature_3_Id, creature_3_min_count, creature_3_max_count, creature_4_Id, creature_4_min_count, creature_4_max_count, " +
                "creature_5_Id, creature_5_min_count, creature_5_max_count, creature_6_Id, creature_6_min_count, creature_6_max_count, radius) " +
                $"SELECT {id}, 1, 0, {respawn}, {N(spot.X)}, {N(y)}, {N(spot.Z)}, {N(spot.Rotation)}, {MapContextId}, {creature}, 1, 1, " +
                "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 " +
                $"FROM creature c WHERE c.id = {creature} AND EXISTS (SELECT 1 FROM {ControlPointEntry.TableName} p WHERE p.id = {spot.Point}) " +
                $"AND NOT EXISTS (SELECT 1 FROM spawnpool x WHERE x.id = {id});";
            yield return Link(spot.Point, kind, id,
                $"SELECT 1 FROM spawnpool s WHERE s.id = {id} AND s.map_context_id = {MapContextId} AND s.creature_1_Id = {creature}");
        }

        private static string Link(uint point, byte kind, uint objectId, string exists) =>
            $"INSERT INTO {ControlPointLinkEntry.TableName} (control_point_id, kind, object_id) " +
            $"SELECT {point}, {kind}, {objectId} FROM {ControlPointEntry.TableName} p WHERE p.id = {point} AND EXISTS ({exists}) " +
            $"AND NOT EXISTS (SELECT 1 FROM {ControlPointLinkEntry.TableName} l WHERE l.control_point_id = {point} AND l.kind = {kind} AND l.object_id = {objectId});";

        private static string PointName(uint point) => point switch
        {
            EdmundRangeSeed.WhiskeyId => "Whiskey",
            EdmundRangeSeed.CharlieId => "Charlie",
            _ => "Echo"
        };

        private static string N(double value) => value.ToString("0.0###", CultureInfo.InvariantCulture);
    }
}
