using System.Collections.Generic;
using System.Globalization;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// What the Stalkers and the Juggernauts were missing (Stalker_and_juggernaut_kits):
    ///  - Every Stalker's stalker_ovulate (CR_ 441) row reached 20 m, the charge's
    ///    EFFECT_RADIUS: a Stalker fights from its blaster's 60 m, so it never laid an egg on a
    ///    player shooting it. The rows now reach 60 m, the egg drop's EFFECT_RADIUS, the reach
    ///    CreatureBombs.Ovulate now asks a player to be in.
    ///  - The Juggernauts had their guns alone. The Plateau's Prototype Juggernaut and Devil's
    ///    Den's get their mortars (CR_JUGGERNAUT_MORTARS 244, the boss its argument 2) and their
    ///    blackhole (CR_JUGGERNAUT_BLACKHOLE 493, JuggernautBlackhole), in empty action slots.
    ///    The client's own numbers are a level's (scale type 2); these rows are at the creature's
    ///    level, as its weapon row is: the mortars twice its gun's hit, the blackhole's burn half
    ///    of it a second. Each comes round every 12 s, the arguments' reuse.
    ///  - Two of the Plateau's four Stalker pools come down from orbit
    ///    (SpawnPoolManager.AnimFromOrbit, anim_type 3): their Stalkers are born where they land,
    ///    the client's 20 s Creature Birth.
    ///
    /// Plain SQL for both providers. Guarded: a row, a slot or a pool that is not as this found
    /// it is left as it is, both ways.
    /// </summary>
    public static class StalkerAndJuggernautKits
    {
        public const uint OvulateAction = 441;
        public const double ChargeReach = 20, EggReach = 60;

        /// <summary>The stalker_ovulate rows: the Marshes', the Plains Overlord's, Devil's Den's two, the Plateau's (and the Energy Weapon Center's).</summary>
        public static readonly uint[] Ovulates = { 65042, 78012, 567160, 567163, 750020 };

        public const uint PrototypeJuggernaut = 750026, DevilsDenJuggernaut = 567038;
        public const uint PrototypeMortars = 790001, PrototypeBlackhole = 790002, DenMortars = 790003, DenBlackhole = 790004;

        /// <summary>An action row: id, description, action, arg, range, cooldown, windup, damage, type.</summary>
        public sealed record Row(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint Windup, uint MinDamage, uint MaxDamage, uint DamageType);

        public static readonly Row[] Added =
        {
            // The Prototype Juggernaut's gun is 1/180 at 260-390.
            new(PrototypeMortars, "Prototype Juggernaut - Plateau mortars 244/2", 244, 2, 10, 60, 12000, 0, 520, 780, 1),
            new(PrototypeBlackhole, "Prototype Juggernaut - Plateau blackhole 493/1", 493, 1, 0, 60, 12000, 0, 130, 195, 6),
            // Devil's Den's is 1/180 at 72-108.
            new(DenMortars, "Juggernaut - Devil's Den mortars 244/1", 244, 1, 10, 60, 12000, 0, 144, 216, 1),
            new(DenBlackhole, "Juggernaut - Devil's Den blackhole 493/1", 493, 1, 0, 60, 12000, 0, 36, 54, 6)
        };

        /// <summary>Which Juggernaut's empty slot takes a new row.</summary>
        public static readonly (uint Creature, string Slot, uint Action)[] Slots =
        {
            (PrototypeJuggernaut, "action2", PrototypeMortars),
            (PrototypeJuggernaut, "action3", PrototypeBlackhole),
            (DevilsDenJuggernaut, "action3", DenMortars),
            (DevilsDenJuggernaut, "action4", DenBlackhole)
        };

        public const uint Plateau = 1497;
        public const short AnimNone = 0, AnimFromOrbit = 3;

        /// <summary>The Plateau Stalker pools that come down from orbit (of 750021-750024).</summary>
        public static readonly uint[] OrbitalPools = { 750022, 750024 };

        private static string Ids(IEnumerable<uint> ids) => string.Join(", ", ids);

        public static IEnumerable<string> Up()
        {
            yield return $"UPDATE creature_action SET range_max = {N(EggReach)} " +
                $"WHERE id IN ({Ids(Ovulates)}) AND action_id = {OvulateAction} AND range_max = {N(ChargeReach)};";

            foreach (var a in Added)
                yield return "INSERT INTO creature_action (id, description, action_id, action_arg_id, range_min, range_max, cooldown, windup, min_damage, max_damage, damage_type) " +
                    $"SELECT {a.Id}, {S(a.Description)}, {a.ActionId}, {a.ArgId}, {N(a.RangeMin)}, {N(a.RangeMax)}, {a.Cooldown}, {a.Windup}, {a.MinDamage}, {a.MaxDamage}, {a.DamageType} " +
                    $"FROM creature c WHERE c.id = {PrototypeJuggernaut} AND NOT EXISTS (SELECT 1 FROM creature_action x WHERE x.id = {a.Id});";

            foreach (var (creature, slot, action) in Slots)
                yield return $"UPDATE creature SET {slot} = {action} WHERE id = {creature} AND {slot} = 0 " +
                    $"AND EXISTS (SELECT 1 FROM creature_action a WHERE a.id = {action});";

            yield return $"UPDATE spawnpool SET anim_type = {AnimFromOrbit} " +
                $"WHERE id IN ({Ids(OrbitalPools)}) AND map_context_id = {Plateau} AND anim_type = {AnimNone};";
        }

        public static IEnumerable<string> Down()
        {
            yield return $"UPDATE spawnpool SET anim_type = {AnimNone} " +
                $"WHERE id IN ({Ids(OrbitalPools)}) AND map_context_id = {Plateau} AND anim_type = {AnimFromOrbit};";

            foreach (var (creature, slot, action) in Slots)
                yield return $"UPDATE creature SET {slot} = 0 WHERE id = {creature} AND {slot} = {action};";

            foreach (var a in Added)
                yield return $"DELETE FROM creature_action WHERE id = {a.Id} AND action_id = {a.ActionId} " +
                    $"AND NOT EXISTS (SELECT 1 FROM creature c WHERE {a.Id} IN (c.action1, c.action2, c.action3, c.action4, c.action5, c.action6, c.action7, c.action8));";

            yield return $"UPDATE creature_action SET range_max = {N(ChargeReach)} " +
                $"WHERE id IN ({Ids(Ovulates)}) AND action_id = {OvulateAction} AND range_max = {N(EggReach)};";
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string S(string value) => "'" + value.Replace("'", "''") + "'";
    }
}
