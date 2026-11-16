using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Services.Preloader
{
    using static PurgasStationContent;

    /// <summary>
    /// Purgas Station below the top floor (map 1806; Purgas_station_lower_levels), as Ten Ton Hammer's
    /// walkthrough tells it, for A Means to an End and Reactor Overload (PurgasStationDepthsMissionsV1)
    /// and the Game's PurgasStation:
    ///
    ///  - The seven fuel basins A Means to an End shuts down (raw fuel pump houses): two in the north-west
    ///    room, one by the transporter at (-112, 35, 33), two on the fuel pump room's catwalks, and the
    ///    last two on the bottom floor's south corridor (-18, 9, -211) and (-120, 9, -214).
    ///  - The reactor's four fuel pumps round its core on the bottom floor, and the stabilization module's
    ///    control on the north control room's upper level (14, 31, 86), for Reactor Overload.
    ///  - The Thrax between the last two basins who carries the Level 3 Access Keypass both want.
    ///  - The pedestal room (-241, 49, -8): five pedestals that, pressed in the order 1 3 4 2 5 from the
    ///    left, take down the field across its east doorway; a wrong one calls Bane security. Past it the
    ///    sleeping pods: shot open, the live one lets out the second Thrax Deathtribe Soldier, whose pool
    ///    is made scripted for it.
    ///  - The trip wires the map's emitters stand at: the "hopscotch" on the floor past the meat door
    ///    (204-212, 81, 189-195, four beams a jump clears), the three stacked across the control room's
    ///    way in (179.5, 65-69, 83-93) and the three across the level-two hall at (-121 to -135, 49-52, 64).
    ///    Crossing one calls Bane to it.
    ///  - Juggernaut Bay's Juggernauts (level 42), and its placeholder pool off the round platform in its
    ///    middle - floor the navmesh joins to nothing - onto the bay's floor.
    ///
    /// Ours: where each stands (on the navmesh), the classes (raw fuel pump houses for the basins, the
    /// reactor plasma pumps, Bane console switches for the pedestals and the module, the Bane trip wire),
    /// the numbers, and the Juggernaut a Prototype Juggernaut's attacks at level 42.
    ///
    /// Ids continue PurgasStationContent's 581xxx. Plain SQL, valid on SQLite and MySQL alike; a row goes
    /// in only where its id is free, and a row is changed only from what it was.
    /// </summary>
    public static class PurgasStationDepthsContent
    {
        // ---- Creatures ------------------------------------------------------------------------

        public const uint KeypassThraxThree = 581012, Juggernaut = 581020;
        public const uint JuggernautCannon = 581001, JuggernautMortars = 581002;

        public sealed record Attack(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint MinDamage, uint MaxDamage, uint DamageType);

        /// <summary>The Prototype Juggernaut's (level 32) cannon and mortars, at level 42.</summary>
        public static readonly Attack[] Attacks =
        {
            new(JuggernautCannon, "Juggernaut - Purgas Station weapon 1/180", 1, 180, 1.0, 60.0, 1500, 350, 520, 1),
            new(JuggernautMortars, "Juggernaut - Purgas Station mortars 244/2", 244, 2, 10.0, 60.0, 12000, 700, 1050, 1),
        };

        public static readonly Being[] Creatures =
        {
            new(KeypassThraxThree, "Thrax Soldier - Purgas Station, the Level 3 keypass", 20757, Hostile, 41, 6878, 205, 239, 9, 5, 0, null, 53001, 53002),
            new(Juggernaut, "Juggernaut - Purgas Station, Juggernaut Bay", 7722, Hostile, 42, 30000, 400, 0, 9, 5, 0, null, JuggernautCannon, JuggernautMortars),
        };

        // ---- The basins, the reactor and the module ----------------------------------------------

        public const uint BasinClass = 10190, ReactorPumpClass = 4443, ConsoleSwitchClass = 7468;
        public const uint BasinHitPoints = 9000, ReactorPumpHitPoints = 6000;

        public static uint BasinId(uint number) => 581010 + number;
        public static uint ReactorPumpId(uint number) => 581020 + number;

        public static readonly (Spot At, string Where)[] Basins =
        {
            (new(-195.0, 33.85, 61.0), "the north-west room"),
            (new(-256.0, 33.87, 64.0), "the north-west room, the far side"),
            (new(-112.0, 35.77, 33.0), "by the transporter"),
            (new(-248.0, 33.81, -100.0), "the fuel pump room, the north catwalk"),
            (new(-234.8, 32.13, -121.4), "the fuel pump room, the south catwalk"),
            (new(-18.0, 9.80, -211.0), "the bottom floor's south corridor, east"),
            (new(-120.0, 9.73, -214.0), "the bottom floor's south corridor, west"),
        };

        public static readonly Spot[] ReactorPumps =
        {
            new(20.0, 2.59, 20.0), new(20.0, 2.75, -20.0), new(-20.0, 2.61, -20.0), new(-20.0, 2.61, 20.0)
        };

        /// <summary>The stabilization module's control, on the north control room's upper level.</summary>
        public const uint StabilizationModule = 581003;
        public static readonly Spot ModuleAt = new(14.0, 31.91, 86.0);

        public static readonly Spot KeypassThreeAt = new(-70.0, 9.91, -212.0);

        // ---- The pedestal room and the sleeping pods ---------------------------------------------

        public const uint FirstPedestal = 581011;
        public static uint PedestalId(uint number) => FirstPedestal + number - 1;

        /// <summary>The order the walkthrough gives, the pedestals numbered from the left facing the field.</summary>
        public static readonly uint[] PedestalOrder = { 1, 3, 4, 2, 5 };

        /// <summary>Facing east, the field ahead: the left is +z. One every 1.75 m across the room.</summary>
        public static Spot PedestalAt(uint number) => new(-234.0, 49.13, -4.5 - (number - 1) * 1.75);

        /// <summary>How high its switch is worked, over the floor.</summary>
        public const double PedestalHeight = 1.2;

        /// <summary>The pedestal room's east doorway: the service jamb at (-224, 48, -8).</summary>
        public static readonly Spot PodDoor = new(-224.0, 49.33, -8.0);

        public const uint PodField = 581002;

        /// <summary>The field's yaw: local x along world -z. Its mesh runs local x -10.85 to 2.85, so its origin is 4 m south.</summary>
        public const double PodFieldYaw = Math.PI / 2;
        public static readonly Spot PodFieldAt = new(PodDoor.X, PodDoor.Y, PodDoor.Z - 4.0);

        public static uint PodId(uint number) => 581030 + number;
        public const uint PodHitPoints = 2500;

        /// <summary>The pods past the field, and which holds the Deathtribe.</summary>
        public static readonly (uint ClassId, Spot At)[] Pods =
        {
            (20621, new(-219.0, 49.50, -11.0)),
            (20661, new(-214.1, 49.05, -14.3)),
            (20760, new(-218.2, 47.13, -21.0)),
            (20621, new(-213.6, 47.53, -20.6)),
        };

        public const uint LivePod = 2;

        // ---- The trip wires ----------------------------------------------------------------------

        public const uint TripWireClass = 10415, TsOn = 56;

        /// <summary>
        /// A set of beams between the map's emitters: which way they run, where each is, across what
        /// span, and how high a crossing foot must be to clear it (none clears a stack).
        /// </summary>
        public sealed record Wire(uint Number, string Where, bool AlongX, double From, double To,
            double[] Lines, double[] Heights, double FloorY, bool Jumpable, uint AlarmPool, Spot AlarmAt);

        public static readonly Wire[] Wires =
        {
            // Past the meat door: four along x at z 189.75-195, on the floor.
            new(1, "the hopscotch past the meat door at (251, 73, 216)", true, 204.24, 212.11,
                new[] { 189.75, 191.49, 193.25, 195.04 }, new[] { 81.16, 81.17, 81.17, 81.14 }, 81.16, true,
                581141, new(208.0, 81.37, 198.0)),
            // The control room's way in: three stacked along z at x 179.52.
            new(2, "the control room's way in at (186, 65, 87)", false, 83.03, 92.92,
                new[] { 179.52 }, new[] { 65.0, 67.0, 69.0 }, 65.49, false,
                581142, new(170.0, 64.20, 88.0)),
            // The level-two hall: three stacked along x at z 64.12.
            new(3, "the level-two hall at (-128, 49, 64)", true, -134.92, -120.85,
                new[] { 64.12 }, new[] { 49.12, 50.43, 51.84 }, 49.13, false,
                581143, new(-128.0, 49.13, 58.6)),
        };

        public static uint WireBeamId(uint wire, int beam) => 581020 + wire * 10 + (uint)beam;

        // ---- Spawn pools ------------------------------------------------------------------------

        public const uint KeypassThreePool = 581132, PedestalSecurityPool = 581144;
        public const uint JuggernautPoolA = 581151, JuggernautPoolB = 581152;

        /// <summary>The Thunderhead's Thrax Soldier and Technician: the alarms' and the pedestals' Bane.</summary>
        public const uint ThunderheadTechnician = 531103;

        public static readonly Pool[] Pools = BuildPools().ToArray();

        private static IEnumerable<Pool> BuildPools()
        {
            yield return new(KeypassThreePool, KeypassThraxThree, Automatic, 3000, KeypassThreeAt, 0, 1, 0, "The Thrax with the Level 3 keypass, between the last two basins");
            foreach (var wire in Wires)
                yield return new(wire.AlarmPool, ThunderheadThrax, Scripted, 10, wire.AlarmAt, 0, 3, 3, $"Trip wire {wire.Number}'s alarm: {wire.Where}");
            yield return new(PedestalSecurityPool, ThunderheadTechnician, Scripted, 10, new(-238.0, 49.13, -8.0), 0, 2, 2, "The pedestal room's security, a wrong pedestal's");
            yield return new(JuggernautPoolA, Juggernaut, Automatic, 6000, new(290.0, 64.13, 160.0), 0, 1, 0, "Juggernaut Bay: a Juggernaut");
            yield return new(JuggernautPoolB, Juggernaut, Automatic, 6000, new(340.0, 64.13, 170.0), 0, 1, 0, "Juggernaut Bay: a Juggernaut");
        }

        // ---- Existing rows changed ----------------------------------------------------------------

        /// <summary>The second Deathtribe waits in his pod.</summary>
        public static uint PodDeathtribePool => DeathtribePool(2);

        /// <summary>Juggernaut Bay's placeholder pool, off the platform the navmesh joins to nothing.</summary>
        public const uint BayPool = 530038;
        public static readonly Spot BayPoolWas = new(300.29998779296875, 66.80000305175781, 138.5);   // as the seed stored it
        public static readonly Spot BayPoolAt = new(320.0, 64.20, 120.0);

        // ---- The SQL ---------------------------------------------------------------------------

        public sealed record Usable(uint Id, uint ClassId, Spot At, double Height, double Rotation, uint State, bool IsUsable, string Comment);

        public static readonly Usable[] Usables = BuildUsables().ToArray();

        private static IEnumerable<Usable> BuildUsables()
        {
            yield return new(StabilizationModule, ConsoleSwitchClass, ModuleAt, PedestalHeight, 0, TsOff, true,
                "Purgas Station: the stabilization module's control, the north control room");

            for (uint n = 1; n <= 5; n++)
                yield return new(PedestalId(n), ConsoleSwitchClass, PedestalAt(n), PedestalHeight, Math.PI / 2, TsOff, true,
                    $"Purgas Station: pedestal {n} of the pedestal room");

            foreach (var wire in Wires)
                for (var beam = 0; beam < Math.Max(wire.Lines.Length, wire.Heights.Length); beam++)
                {
                    var line = wire.Lines[Math.Min(beam, wire.Lines.Length - 1)];
                    var height = wire.Heights[Math.Min(beam, wire.Heights.Length - 1)];
                    var middle = (wire.From + wire.To) / 2;
                    var at = wire.AlongX ? new Spot(middle, height, line) : new Spot(line, height, middle);
                    yield return new(WireBeamId(wire.Number, beam), TripWireClass, at, 0, wire.AlongX ? 0 : Math.PI / 2, TsOn, false,
                        $"Purgas Station: trip wire {wire.Number}, beam {beam + 1}: {wire.Where}");
                }
        }

        public sealed record Destructible(uint Id, uint ClassId, Spot At, double Rotation, uint HitPoints, uint Side, bool Sealed, string Comment);

        public static readonly Destructible[] Destructibles = BuildDestructibles().ToArray();

        private static IEnumerable<Destructible> BuildDestructibles()
        {
            for (uint n = 1; n <= Basins.Length; n++)
                yield return new(BasinId(n), BasinClass, Basins[n - 1].At, 0, BasinHitPoints, 0, false,
                    $"Purgas Station: fuel basin {n}, {Basins[n - 1].Where}");

            for (uint n = 1; n <= ReactorPumps.Length; n++)
                yield return new(ReactorPumpId(n), ReactorPumpClass, ReactorPumps[n - 1], 0, ReactorPumpHitPoints, 0, false,
                    $"Purgas Station: the reactor's fuel pump {n}");

            yield return new(PodField, FieldClass, PodFieldAt, PodFieldYaw, 0, 1, true,
                "Purgas Station: the Bane force field across the pedestal room's east doorway");

            for (uint n = 1; n <= Pods.Length; n++)
                yield return new(PodId(n), Pods[n - 1].ClassId, Pods[n - 1].At, 0, PodHitPoints, 0, false,
                    $"Purgas Station: sleeping pod {n}{(n == LivePod ? ", the Deathtribe's" : "")}");
        }

        private const string Slots =
            "creature_1_Id, creature_1_min_count, creature_1_max_count, creature_2_Id, creature_2_min_count, creature_2_max_count, " +
            "creature_3_Id, creature_3_min_count, creature_3_max_count, creature_4_Id, creature_4_min_count, creature_4_max_count, " +
            "creature_5_Id, creature_5_min_count, creature_5_max_count, creature_6_Id, creature_6_min_count, creature_6_max_count";

        private const string CreatureColumns = "id, comment, class_id, faction, level, max_hp, name_id, run_speed, walk_speed, " +
            "action1, action2, action3, action4, action5, action6, action7, action8";

        private static string InPool(uint creature) =>
            $"{creature} IN (p.creature_1_Id, p.creature_2_Id, p.creature_3_Id, p.creature_4_Id, p.creature_5_Id, p.creature_6_Id)";

        public static IEnumerable<string> Up()
        {
            foreach (var a in Attacks)
                yield return "INSERT INTO creature_action (id, description, action_id, action_arg_id, range_min, range_max, cooldown, windup, min_damage, max_damage, damage_type) " +
                    $"SELECT {a.Id}, {S(a.Description)}, {a.ActionId}, {a.ArgId}, {N(a.RangeMin)}, {N(a.RangeMax)}, {a.Cooldown}, 0, {a.MinDamage}, {a.MaxDamage}, {a.DamageType} " +
                    $"FROM (SELECT 1 AS one) d WHERE NOT EXISTS (SELECT 1 FROM creature_action x WHERE x.id = {a.Id});";

            foreach (var c in Creatures)
            {
                var actions = Enumerable.Range(0, 8).Select(i => i < c.Actions.Length ? c.Actions[i] : 0U).ToArray();
                yield return $"INSERT INTO creature ({CreatureColumns}) " +
                    $"SELECT {c.Id}, {S(c.Comment)}, {c.ClassId}, {c.Faction}, {c.Level}, {c.Hp}, {c.NameId}, {c.RunSpeed}, {c.WalkSpeed}, {string.Join(", ", actions)} " +
                    $"FROM entityclass e WHERE e.id = {c.ClassId} AND NOT EXISTS (SELECT 1 FROM creature x WHERE x.id = {c.Id});";
                yield return $"INSERT INTO creature_stat (id, body, mind, spirit, health, armor) SELECT {c.Id}, 15, 15, 15, {c.Hp}, {c.Armor} " +
                    $"FROM creature x WHERE x.id = {c.Id} AND NOT EXISTS (SELECT 1 FROM creature_stat s WHERE s.id = {c.Id});";
            }

            foreach (var p in Pools)
                yield return "INSERT INTO spawnpool (id, mode, anim_type, respown_time, pos_x, pos_y, pos_z, rotation, map_context_id, " + Slots + ", radius) " +
                    $"SELECT {p.Id}, {p.Mode}, 0, {p.Respawn}, {N(p.At.X)}, {N(p.At.Y)}, {N(p.At.Z)}, {N(p.Rotation)}, {Map}, " +
                    $"{p.Creature}, {p.Count}, {p.Count}, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, {N(p.Radius)} " +
                    $"FROM creature c WHERE c.id = {p.Creature} AND NOT EXISTS (SELECT 1 FROM spawnpool x WHERE x.id = {p.Id});";

            yield return $"UPDATE spawnpool SET mode = {Scripted} WHERE id = {PodDeathtribePool} AND map_context_id = {Map} AND creature_1_Id = {Deathtribe} AND mode = {Automatic};";
            yield return MoveBayPool(BayPoolWas, BayPoolAt);

            foreach (var u in Usables)
                yield return "INSERT INTO world_usable (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, state, usable, item_template_id, item_class_id, " +
                    "item_count, reset_seconds, interval_min_seconds, interval_max_seconds, damage_min, damage_max, radius, action_arg, comment) " +
                    $"SELECT {u.Id}, {Map}, {u.ClassId}, {N(u.At.X)}, {N(u.At.Y + u.Height)}, {N(u.At.Z)}, {N(u.Rotation)}, {u.State}, {(u.IsUsable ? 1 : 0)}, 0, 0, " +
                    $"0, 0, 0, 0, 0, 0, 0, 0, {S(u.Comment)} " +
                    $"FROM entityclass e WHERE e.id = {u.ClassId} AND NOT EXISTS (SELECT 1 FROM world_usable x WHERE x.id = {u.Id});";

            foreach (var d in Destructibles)
                yield return "INSERT INTO world_destructible (id, map_context_id, class_id, pos_x, pos_y, pos_z, rotation, hit_points, creature_id, creature_count, side, comment, sealed) " +
                    $"SELECT {d.Id}, {Map}, {d.ClassId}, {N(d.At.X)}, {N(d.At.Y)}, {N(d.At.Z)}, {N(d.Rotation)}, {d.HitPoints}, 0, 0, {d.Side}, {S(d.Comment)}, {(d.Sealed ? 1 : 0)} " +
                    $"FROM entityclass e WHERE e.id = {d.ClassId} AND NOT EXISTS (SELECT 1 FROM world_destructible x WHERE x.id = {d.Id});";
        }

        public static IEnumerable<string> Down()
        {
            foreach (var d in Destructibles)
                yield return $"DELETE FROM world_destructible WHERE id = {d.Id} AND map_context_id = {Map} AND class_id = {d.ClassId};";

            foreach (var u in Usables)
                yield return $"DELETE FROM world_usable WHERE id = {u.Id} AND map_context_id = {Map} AND class_id = {u.ClassId};";

            yield return MoveBayPool(BayPoolAt, BayPoolWas);
            yield return $"UPDATE spawnpool SET mode = {Automatic} WHERE id = {PodDeathtribePool} AND map_context_id = {Map} AND creature_1_Id = {Deathtribe} AND mode = {Scripted};";

            foreach (var p in Pools)
                yield return $"DELETE FROM spawnpool WHERE id = {p.Id} AND map_context_id = {Map} AND creature_1_Id = {p.Creature};";

            foreach (var c in Creatures)
            {
                yield return $"DELETE FROM creature_stat WHERE id = {c.Id};";
                yield return $"DELETE FROM creature WHERE id = {c.Id} AND name_id = {c.NameId} AND NOT EXISTS (SELECT 1 FROM spawnpool p WHERE {InPool(c.Id)});";
            }

            foreach (var a in Attacks)
                yield return $"DELETE FROM creature_action WHERE id = {a.Id} " +
                    "AND NOT EXISTS (SELECT 1 FROM creature c WHERE " +
                    $"{a.Id} IN (c.action1, c.action2, c.action3, c.action4, c.action5, c.action6, c.action7, c.action8));";
        }

        private static string MoveBayPool(Spot from, Spot to) =>
            $"UPDATE spawnpool SET pos_x = {N(to.X)}, pos_y = {N(to.Y)}, pos_z = {N(to.Z)} " +
            $"WHERE id = {BayPool} AND map_context_id = {Map} " +
            $"AND ABS(pos_x - {N(from.X)}) < 0.05 AND ABS(pos_y - {N(from.Y)}) < 0.05 AND ABS(pos_z - {N(from.Z)}) < 0.05;";
    }
}
