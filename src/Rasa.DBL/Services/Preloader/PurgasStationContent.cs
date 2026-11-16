using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Purgas Station's top floor peopled and fitted out (adv_foreas_concordia_divide_purgasstation2,
    /// map 1806; Purgas_station_level_one), as Ten Ton Hammer's walkthrough tells it, for the missions
    /// PurgasStationMissionsV1 authors and the Game's PurgasStation runs:
    ///
    ///  - Squad Leader Keen and two of his squad in the holding room left of the Entrance Hall's
    ///    corridor (the 16 m service room at (192, 64, 168)), behind a Bane force field across its
    ///    doorway that no shot takes down: the Force Field Controller on the control room's overlook
    ///    is what drops it.
    ///  - Comm. Expert Taylor and Information Specialists Gates and Hauffman on the overlook, "ported
    ///    in" once A New Plan of Attack is done in the copy (their pools are scripted).
    ///  - The Waypoint Activation Terminal in the control room's lower level, and the "Level 2:
    ///    Auxiliary Storage Chamber" waypoint (teleporter 79, a row with no map until now) in the
    ///    service room south of the control room, which the terminal gives.
    ///  - The five Thrax Deathtribe Soldiers (level 45) Chain Reaction counts: the control room,
    ///    past the pedestal room, the fuel pool room's east bump-out, the lowest control room's
    ///    west gallery and the reactor's north control room.
    ///  - The non-descript Thrax in the drop-out room off the circuit corridor, whose drop is Off
    ///    Grid's Level 2 Access Keypass.
    ///
    /// And the station levelled for the squads "in the early 40s" it is for: the Divide seed's
    /// thirteen placeholder pools here held the Divide's level 15-18 Thrax, Grenadiers, Technicians,
    /// Caretakers, Shield Drones, Hunters and their lieutenant and a Thrax Commander, and its seven
    /// mortars the Divide's level 18 one. Their slots are pointed at the level 40-43 rows of the
    /// same kinds (the Thunderhead's, mostly; <see cref="Retargets"/>); the pools' places, modes and
    /// counts are left as they are.
    ///
    /// From the client: the map (the holding room, the overlook's consoles, the service rooms), the
    /// names (Keen 6721, Taylor 6743, Gates 7060, Hauffman 7061, Thrax Deathtribe Soldier 6741), the
    /// keypass (MisDivideItemPurgasLevel2Pass, class 20745, template 42229) and the waypoint's name.
    /// Ours, where the walkthrough gives no more than a room: where each stands (on the navmesh),
    /// the classes standing in for the release and activation terminals (the Bane switch) and the
    /// cell's field (the Bane outpost wall's minor entrance field, sealed), the numbers, and
    /// the Deathtribe a Thrax Commander's class and attacks at level 45.
    ///
    /// Ids 581001 on: creatures 5810xx, pools 5811xx, usables and the field 5810xx. Plain SQL, valid
    /// on SQLite and MySQL alike. A row goes in only where its id is free, and a row is changed only
    /// from what it was; Down takes out and puts back only as Up left them.
    /// </summary>
    public static class PurgasStationContent
    {
        public const uint Map = 1806;

        public const uint Stevens = 510110, StevensPackage = 586;

        // ---- Creatures --------------------------------------------------------------------

        public const uint Keen = 581001, Taylor = 581002, Gates = 581003, Hauffman = 581004, Trooper = 581005;
        public const uint Deathtribe = 581010, KeypassThrax = 581011;

        public const uint KeenPackage = 578, TaylorPackage = 613;

        public const uint Hostile = 0, Friendly = 1;

        /// <summary>A creature row: its class, numbers, attacks (creature_action ids) and whose look it wears.</summary>
        public sealed record Being(uint Id, string Comment, uint ClassId, uint Faction, uint Level, uint Hp, uint Armor,
            uint NameId, uint RunSpeed, uint WalkSpeed, uint LookFrom, uint? Package, params uint[] Actions);

        // Looks from AFS officers already dressed: Captain Rankin, Field Lt. Quinn, Sergeant Spencer,
        // Commander Elvers. The Deathtribe's attacks are the Thunderhead Thrax Commander's (level 43),
        // the keypass Thrax's the Thunderhead Thrax Soldier's (level 40).
        public static readonly Being[] Creatures =
        {
            new(Keen, "Squad Leader Keen - Purgas Station", 3846, Friendly, 40, 1000, 0, 6721, 0, 0, 510100, KeenPackage),
            new(Taylor, "Comm. Expert Taylor - Purgas Station", 3846, Friendly, 40, 1000, 0, 6743, 0, 0, 510106, TaylorPackage),
            new(Gates, "Information Spec. Gates - Purgas Station", 3846, Friendly, 40, 1000, 0, 7060, 0, 0, 510112, null),
            new(Hauffman, "Information Spec. Hauffman - Purgas Station", 3846, Friendly, 40, 1000, 0, 7061, 0, 0, 400403, null),
            new(Trooper, "AFS Infantryman - Purgas Station, Keen's squad", 29423, Friendly, 40, 1400, 0, 8716, 0, 0, 0, null),
            new(Deathtribe, "Thrax Deathtribe Soldier - Purgas Station", 10504, Hostile, 45, 20000, 420, 6741, 9, 5, 0, null, 53161, 53162),
            new(KeypassThrax, "Thrax Soldier - Purgas Station, the Level 2 keypass", 20757, Hostile, 40, 6307, 190, 355, 9, 5, 0, null, 53155, 53156),
        };

        // ---- Where things are ------------------------------------------------------------------

        public sealed record Spot(double X, double Y, double Z);

        /// <summary>The holding room's doorway: the service jamb at (192, 64, 160), its floor.</summary>
        public static readonly Spot CellDoor = new(192.0, 65.33, 160.0);

        public static readonly Spot KeenAt = new(192.0, 65.13, 170.0);
        public static readonly Spot TaylorAt = new(150.0, 73.73, 131.0);
        public static readonly Spot GatesAt = new(146.4, 73.73, 126.2);
        public static readonly Spot HauffmanAt = new(154.0, 73.78, 123.0);

        /// <summary>The Bane CommLink on the overlook's north end (the walkthrough's 160, 73, 130), by the console there.</summary>
        public static readonly Spot CommLinkAt = new(161.0, 73.89, 131.5);
        public const double CommLinkYaw = Math.PI / 2;

        public static readonly Spot ReleaseAt = new(157.5, 73.73, 133.5);
        public static readonly Spot TerminalAt = new(150.0, 63.73, 82.0);

        /// <summary>The service room south of the control room: the "Level 2: Auxiliary Storage Chamber" waypoint.</summary>
        public static readonly Spot StorageChamberAt = new(144.0, 65.27, 24.0);

        /// <summary>The drop-out room off the circuit corridor (the walkthrough's 108, 68, -2): the keypass Thrax.</summary>
        public static readonly Spot DropOutRoomAt = new(104.0, 65.31, -30.0);

        /// <summary>The far, west side of the reactor's top tier, round the green hallway: "explore the facility further".</summary>
        public static readonly Spot FarSideAt = new(-80.0, 68.5, 0.0);

        /// <summary>The five Deathtribe: the walkthrough's rooms, in its order.</summary>
        public static readonly (Spot At, string Where)[] DeathtribeSpots =
        {
            (new(160.0, 63.89, 95.0), "the control room, past the field at (186, 65, 87)"),
            (new(-216.0, 49.33, -16.0), "past the pedestal room (-241, 49, -8): the sleeping pods"),
            (new(-208.0, 33.92, -144.0), "the fuel pump room (-240, 33, -109), its east bump-out"),
            (new(12.0, 17.73, -150.0), "the lowest control room past the door at (94, 9, -93), the west gallery"),
            (new(10.0, 31.73, 92.0), "the control room at (14, 31, 86), by the stabilization panel"),
        };

        // ---- Spawn pools ----------------------------------------------------------------------

        public const short Automatic = 0, Scripted = 2;

        public sealed record Pool(uint Id, uint Creature, short Mode, uint Respawn, Spot At, double Rotation, uint Count, double Radius, string Comment);

        public const uint KeenPool = 581101, TrooperPoolA = 581102, TrooperPoolB = 581103;
        public const uint TaylorPool = 581104, GatesPool = 581105, HauffmanPool = 581106;
        public const uint FirstDeathtribePool = 581111, EscortPool = 581121, KeypassPool = 581131;

        public static uint DeathtribePool(uint number) => FirstDeathtribePool + number - 1;

        /// <summary>The Thunderhead's level 40 Thrax Soldier, the Deathtribe's escort in the control room.</summary>
        public const uint ThunderheadThrax = 531101;

        /// <summary>Half an hour (tenths of a second): an elite killed stays down long after the squad has moved on.</summary>
        public const uint EliteRespawn = 18000;

        public static readonly Pool[] Pools = BuildPools().ToArray();

        private static IEnumerable<Pool> BuildPools()
        {
            yield return new(KeenPool, Keen, Automatic, 20, KeenAt, Math.PI, 1, 0, "Squad Leader Keen in the holding room");
            yield return new(TrooperPoolA, Trooper, Automatic, 20, new(190.0, 65.08, 166.0), Math.PI, 1, 0, "Keen's squad in the holding room");
            yield return new(TrooperPoolB, Trooper, Automatic, 20, new(194.0, 65.08, 166.0), Math.PI, 1, 0, "Keen's squad in the holding room");
            yield return new(TaylorPool, Taylor, Scripted, 20, TaylorAt, 0, 1, 0, "Comm. Expert Taylor on the overlook, once A New Plan of Attack is done");
            yield return new(GatesPool, Gates, Scripted, 20, GatesAt, 0, 1, 0, "Information Spec. Gates on the overlook");
            yield return new(HauffmanPool, Hauffman, Scripted, 20, HauffmanAt, 0, 1, 0, "Information Spec. Hauffman on the overlook");

            for (uint n = 1; n <= DeathtribeSpots.Length; n++)
                yield return new(DeathtribePool(n), Deathtribe, Automatic, EliteRespawn, DeathtribeSpots[n - 1].At, 0, 1, 0,
                    $"Thrax Deathtribe Soldier {n}: {DeathtribeSpots[n - 1].Where}");

            yield return new(EscortPool, ThunderheadThrax, Automatic, 3000, new(156.0, 63.75, 99.0), 0, 2, 3, "The control room's troops, with the first Deathtribe");
            yield return new(KeypassPool, KeypassThrax, Automatic, 3000, DropOutRoomAt, 0, 1, 0, "The drop-out room's Thrax, with the Level 2 keypass");
        }

        // ---- Existing rows changed ------------------------------------------------------------

        /// <summary>The Divide seed's placeholder pools here.</summary>
        public static readonly uint[] PlaceholderPools = Enumerable.Range(530038, 13).Select(id => (uint)id).ToArray();

        /// <summary>The Divide seed's mortar pools here (BaneMortars').</summary>
        public static readonly uint[] MortarPools = Enumerable.Range(592245, 7).Select(id => (uint)id).ToArray();

        /// <summary>The Divide's mortar (BaneMortars' for the zone), and the Thunderhead's the station's pads have instead.</summary>
        public const uint DivideMortar = 592002, StationMortar = 592012;

        /// <summary>Each Divide creature of the placeholders and mortars, and the level-40s one in its place.</summary>
        public static readonly (uint Divide, uint Station, string What)[] Retargets =
        {
            (530001, 531101, "Thrax Soldier: Divide 15, Thunderhead 40"),
            (530002, 531102, "Thrax Grenadier: Divide 15, Thunderhead 40"),
            (530003, 531103, "Thrax Technician: Divide 16, Thunderhead 41"),
            (530004, 531104, "Caretaker: Divide 16, Thunderhead 41"),
            (530005, 531105, "Thrax Commander: Divide 18, Thunderhead 43"),
            (530006, 531106, "Shield Drone: Divide 16, Thunderhead 41"),
            (530007, 531001, "Hunter (Divide 15): the Abyss's Thrax Soldier (41), there being no Hunter in the 40s"),
            (530009, 531012, "Hunter Lieutenant (Divide 18): the Ashen Desert's Thrax Commander (42), the Mires' lieutenant being 39"),
            (DivideMortar, StationMortar, "Bane Mortar: Divide 18, Thunderhead 43"),
        };

        // ---- Usable and destructible objects ----------------------------------------------------

        public const uint SwitchClass = 7467, FieldClass = 20000003;
        public const uint ReleaseSwitch = 581001, WaypointTerminal = 581002;
        public const uint CellField = 581001;
        public const uint TsOff = 55;

        /// <summary>How high on its post a Bane switch is worked, over the floor (Torcastra's cell console's).</summary>
        public const double SwitchHeight = 1.45;

        public sealed record Usable(uint Id, uint ClassId, Spot At, double Rotation, string Comment);

        public static readonly Usable[] Usables =
        {
            new(ReleaseSwitch, SwitchClass, ReleaseAt, Math.PI / 2, "Purgas Station: the Force Field Controller, the holding room's"),
            new(WaypointTerminal, SwitchClass, TerminalAt, 0, "Purgas Station: the Waypoint Activation Terminal"),
        };

        /// <summary>
        /// The field across the holding room's doorway: the minor entrance's mesh runs local x -10.85 to 2.85, so
        /// its origin is 4 m east of the doorway's middle.
        /// </summary>
        public static readonly Spot FieldAt = new(CellDoor.X + 4.0, CellDoor.Y, CellDoor.Z);

        // ---- The waypoint -----------------------------------------------------------------------

        public const uint StorageChamberWaypoint = 79, WaypointClass = 29648, WaypointType = 2;
        public const string StorageChamberName = "Level 2: Auxiliary Storage Chamber";

        // ---- The SQL ---------------------------------------------------------------------------

        private const string Slots =
            "creature_1_Id, creature_1_min_count, creature_1_max_count, creature_2_Id, creature_2_min_count, creature_2_max_count, " +
            "creature_3_Id, creature_3_min_count, creature_3_max_count, creature_4_Id, creature_4_min_count, creature_4_max_count, " +
            "creature_5_Id, creature_5_min_count, creature_5_max_count, creature_6_Id, creature_6_min_count, creature_6_max_count";

        private const string CreatureColumns = "id, comment, class_id, faction, level, max_hp, name_id, run_speed, walk_speed, " +
            "action1, action2, action3, action4, action5, action6, action7, action8";

        private static readonly string[] SlotColumns =
            { "creature_1_Id", "creature_2_Id", "creature_3_Id", "creature_4_Id", "creature_5_Id", "creature_6_Id" };

        private static string InPool(uint creature) =>
            $"{creature} IN (p.creature_1_Id, p.creature_2_Id, p.creature_3_Id, p.creature_4_Id, p.creature_5_Id, p.creature_6_Id)";

        private static string Retargeted => string.Join(", ", PlaceholderPools.Concat(MortarPools));

        public static IEnumerable<string> Up()
        {
            foreach (var c in Creatures)
            {
                var actions = Enumerable.Range(0, 8).Select(i => i < c.Actions.Length ? c.Actions[i] : 0U).ToArray();
                yield return $"INSERT INTO creature ({CreatureColumns}) " +
                    $"SELECT {c.Id}, {S(c.Comment)}, {c.ClassId}, {c.Faction}, {c.Level}, {c.Hp}, {c.NameId}, {c.RunSpeed}, {c.WalkSpeed}, {string.Join(", ", actions)} " +
                    $"FROM entityclass e WHERE e.id = {c.ClassId} AND NOT EXISTS (SELECT 1 FROM creature x WHERE x.id = {c.Id});";

                if (c.Actions.Length > 0)
                    yield return $"INSERT INTO creature_stat (id, body, mind, spirit, health, armor) SELECT {c.Id}, 15, 15, 15, {c.Hp}, {c.Armor} " +
                        $"FROM creature x WHERE x.id = {c.Id} AND NOT EXISTS (SELECT 1 FROM creature_stat s WHERE s.id = {c.Id});";

                if (c.LookFrom != 0)
                    yield return CopyLook(c.Id, c.LookFrom);

                if (c.Package.HasValue)
                    yield return $"INSERT INTO npc_package (id, package_id, comment) SELECT {c.Id}, {c.Package.Value}, {S(Short(c.Comment))} " +
                        $"FROM creature x WHERE x.id = {c.Id} AND NOT EXISTS (SELECT 1 FROM npc_package n WHERE n.id = {c.Id});";
            }

            foreach (var p in Pools)
                yield return "INSERT INTO spawnpool (id, mode, anim_type, respown_time, pos_x, pos_y, pos_z, rotation, map_context_id, " + Slots + ", radius) " +
                    $"SELECT {p.Id}, {p.Mode}, 0, {p.Respawn}, {N(p.At.X)}, {N(p.At.Y)}, {N(p.At.Z)}, {N(p.Rotation)}, {Map}, " +
                    $"{p.Creature}, {p.Count}, {p.Count}, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, {N(p.Radius)} " +
                    $"FROM creature c WHERE c.id = {p.Creature} AND NOT EXISTS (SELECT 1 FROM spawnpool x WHERE x.id = {p.Id});";

            foreach (var (divide, station, _) in Retargets)
                foreach (var slot in SlotColumns)
                    yield return $"UPDATE spawnpool SET {slot} = {station} WHERE id IN ({Retargeted}) AND map_context_id = {Map} AND {slot} = {divide} " +
                        $"AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {station});";

            foreach (var u in Usables)
                yield return "INSERT INTO world_usable (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, state, usable, item_template_id, item_class_id, " +
                    "item_count, reset_seconds, interval_min_seconds, interval_max_seconds, damage_min, damage_max, radius, action_arg, comment) " +
                    $"SELECT {u.Id}, {Map}, {u.ClassId}, {N(u.At.X)}, {N(u.At.Y + SwitchHeight)}, {N(u.At.Z)}, {N(u.Rotation)}, {TsOff}, 1, 0, 0, " +
                    $"0, 0, 0, 0, 0, 0, 0, 0, {S(u.Comment)} " +
                    $"FROM entityclass e WHERE e.id = {u.ClassId} AND NOT EXISTS (SELECT 1 FROM world_usable x WHERE x.id = {u.Id});";

            yield return "INSERT INTO world_destructible (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, hit_points, creature_id, creature_count, side, comment, sealed) " +
                $"SELECT {CellField}, {Map}, {FieldClass}, {N(FieldAt.X)}, {N(FieldAt.Y)}, {N(FieldAt.Z)}, 0, 0, 0, 0, 1, " +
                $"{S("Purgas Station: the Bane force field across the holding room's doorway")}, 1 " +
                $"FROM entityclass e WHERE e.id = {FieldClass} AND NOT EXISTS (SELECT 1 FROM world_destructible x WHERE x.id = {CellField});";

            yield return $"UPDATE teleporter SET class_id = {WaypointClass}, type = {WaypointType}, description = {S("Waypoint: " + StorageChamberName)}, " +
                $"pos_x = {N(StorageChamberAt.X)}, pos_y = {N(StorageChamberAt.Y)}, pos_z = {N(StorageChamberAt.Z)}, rotation = 0, map_context_id = {Map} " +
                $"WHERE id = {StorageChamberWaypoint} AND map_context_id = 0 AND type = 0 AND description = {S(StorageChamberName)};";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"UPDATE teleporter SET class_id = 0, type = 0, description = {S(StorageChamberName)}, pos_x = 0, pos_y = 0, pos_z = 0, rotation = 0, map_context_id = 0 " +
                $"WHERE id = {StorageChamberWaypoint} AND map_context_id = {Map} AND type = {WaypointType} AND description = {S("Waypoint: " + StorageChamberName)};";

            yield return $"DELETE FROM world_destructible WHERE id = {CellField} AND map_context_id = {Map} AND class_id = {FieldClass};";

            foreach (var u in Usables)
                yield return $"DELETE FROM world_usable WHERE id = {u.Id} AND map_context_id = {Map} AND class_id = {u.ClassId};";

            foreach (var (divide, station, _) in Retargets)
                foreach (var slot in SlotColumns)
                    yield return $"UPDATE spawnpool SET {slot} = {divide} WHERE id IN ({Retargeted}) AND map_context_id = {Map} AND {slot} = {station};";

            foreach (var p in Pools)
                yield return $"DELETE FROM spawnpool WHERE id = {p.Id} AND map_context_id = {Map} AND creature_1_Id = {p.Creature};";

            foreach (var c in Creatures)
            {
                if (c.Package.HasValue)
                    yield return $"DELETE FROM npc_package WHERE id = {c.Id} AND package_id = {c.Package.Value};";
                if (c.LookFrom != 0)
                    yield return $"DELETE FROM creature_appearance WHERE id = {c.Id};";
                if (c.Actions.Length > 0)
                    yield return $"DELETE FROM creature_stat WHERE id = {c.Id};";
                yield return $"DELETE FROM creature WHERE id = {c.Id} AND name_id = {c.NameId} AND NOT EXISTS (SELECT 1 FROM spawnpool p WHERE {InPool(c.Id)});";
            }
        }

        private static string CopyLook(uint creature, uint from) =>
            $"INSERT INTO creature_appearance (id, slot_id, Class_id, color) SELECT {creature}, a.slot_id, a.Class_id, a.color " +
            $"FROM creature_appearance a WHERE a.id = {from} AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {creature}) " +
            $"AND NOT EXISTS (SELECT 1 FROM creature_appearance x WHERE x.id = {creature});";

        private static string Short(string comment) => comment.Length <= 50 ? comment : comment.Substring(0, 50);

        internal static string S(string value) => "'" + value.Replace("'", "''") + "'";

        internal static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
