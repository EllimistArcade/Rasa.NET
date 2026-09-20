using System;
using System.Threading;

namespace Rasa.Managers
{
    using Data;
    using Structures;

    /// <summary>
    /// Stunning creatures. A stun is a GameEffect flagged IsStun; while one is on a creature it
    /// neither moves nor attacks (BehaviorManager), and the client's StunEffect puts it in its
    /// uncontrolled state with movement blocked. Players are not stunned yet.
    ///
    /// Where stuns come from, and the numbers:
    ///
    ///  - Abilities, from their action data. Lightning pumps 4, 5 and 7: STUN_CHANCE 50,
    ///    STUN_DURATION 3 (seconds). Tectonic Strike: DURATION_MS 4000-8000, the tooltip's "Stun
    ///    Duration: 4s".."8s". Concussive Wave: DURATION 10, "Stun Duration: 10s". Rushing Blow:
    ///    DURATION 1-5, its "knockback, and stun". Those three always stun. Effect STUN 86.
    ///  - Critical hits by Ice (CRIT_ICE 3, "Frozen", a StunEffect) and Sonic (CRIT_SONIC 7,
    ///    "Stunned", a KnockbackEffect whose OnAttach takes the duration). How long is not in the
    ///    client: CritStunMs.
    ///  - Hand to Hand on melee hits: pump 4 "Knockback Chance: +50%, Stun Duration: 1s", pump 5
    ///    "+75%, 2s". Taken as a stun at the knockback chance, since there is no knockback yet.
    ///  - Launchers on grenades: "Grenades: +25% / +40% / +50% Stun Chance" at pumps 3-5. The
    ///    duration is not given: GrenadeStunMs.
    ///
    /// A stun on a creature at or below Critical Death's health threshold opens its window
    /// (CritDeathManager.TryEnterPreDeath), whichever came first, the stun or the damage.
    /// </summary>
    public static class Stuns
    {
        public const int StunTypeId = 86;           // STUN
        public const int CritIceTypeId = 3;         // CRIT_ICE
        public const int CritSonicTypeId = 7;       // CRIT_SONIC

        /// <summary>How long an Ice or Sonic crit stuns for. Not in the client.</summary>
        public const int CritStunMs = 2000;

        /// <summary>How long a grenade's stun lasts. Not in the client.</summary>
        public const int GrenadeStunMs = 2000;

        private static readonly int[] HandToHandChanceByPump = { 0, 0, 0, 0, 50, 75 };
        private static readonly int[] HandToHandMsByPump = { 0, 0, 0, 0, 1000, 2000 };
        private static readonly int[] GrenadeChanceByPump = { 0, 0, 0, 25, 40, 50 };

        private static readonly ThreadLocal<Random> Rng = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        private static int Pump(int pump) => Math.Max(0, Math.Min(5, pump));

        public static int HandToHandChance(int pump) => HandToHandChanceByPump[Pump(pump)];
        public static int HandToHandMs(int pump) => HandToHandMsByPump[Pump(pump)];
        public static int GrenadeChance(int pump) => GrenadeChanceByPump[Pump(pump)];

        public static bool Roll(int chancePercent)
        {
            if (chancePercent <= 0)
                return false;

            return chancePercent >= 100 || Rng.Value.Next(100) < chancePercent;
        }

        /// <summary>
        /// The stun an ability's hit carries, from its data: (chance, milliseconds), or (0, 0) for
        /// none. STUN_CHANCE/STUN_DURATION where the ability has them; otherwise the duration
        /// Tectonic Strike, Concussive Wave and Rushing Blow keep in DURATION_MS or DURATION.
        /// </summary>
        public static (int Chance, int Ms) OfAbility(string module, ActionLevelInfo info)
        {
            if (info.Has(AbilityProperty.StunDuration))
                return (info.Get(AbilityProperty.StunChance, 100), info.Get(AbilityProperty.StunDuration) * 1000);

            switch (module)
            {
                case "abilities.tectonicstrike":
                    return (100, info.Get(AbilityProperty.DurationMs));
                case "abilities.concussivewave":
                case "abilities.rushingblow":
                    return (100, info.Get(AbilityProperty.Duration) * 1000);
                default:
                    return (0, 0);
            }
        }

        /// <summary>The crit stun for a damage type: Ice freezes, Sonic stuns; 0 for the others.</summary>
        public static int CritStunTypeFor(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Ice:
                    return CritIceTypeId;
                case DamageType.Sonic:
                    return CritSonicTypeId;
                default:
                    return 0;
            }
        }

        public static bool IsStunned(Actor actor)
        {
            foreach (var effect in actor.ActiveEffects.Values)
                if (effect.IsStun && !effect.IsExpired)
                    return true;

            return false;
        }

        /// <summary>
        /// Stuns a creature for durationMs with the given effect type. A second stun of the same
        /// type starts the clock over. Afterwards the creature's Critical Death window opens if it
        /// is already low enough. damageType is the hit's, for the window's death animation.
        /// </summary>
        public static bool Apply(MapChannel mapChannel, Creature target, Actor source, int typeId, int durationMs, DamageType damageType)
        {
            if (target == null || durationMs <= 0 || target.State == CharacterState.Dead || target.State == CharacterState.Dying)
                return false;

            if (!target.Attributes.TryGetValue(Attributes.Health, out var health) || health.Current <= 0)
                return false;

            var stun = new GameEffect
            {
                TypeId = typeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = 1,
                SourceId = source?.EntityId ?? 0,
                Source = source,
                SourceLevel = (source as Manifestation)?.Level ?? 1,
                IsBuff = false,
                IsStun = true,
                ExpiresTick = Environment.TickCount64 + durationMs
            };

            // The Sonic crit's client class is a KnockbackEffect, whose OnAttach wants the duration.
            if (typeId == CritSonicTypeId)
                GameEffectManager.Instance.Attach(mapChannel, target, stun, durationMs / 1000.0);
            else
                GameEffectManager.Instance.Attach(mapChannel, target, stun);

            CritDeathManager.Instance.TryEnterPreDeath(mapChannel, target, source, damageType);

            return true;
        }

        /// <summary>A critical hit's stun, if its damage type has one.</summary>
        public static void OnCritical(MapChannel mapChannel, Creature target, Actor source, DamageType damageType)
        {
            var typeId = CritStunTypeFor(damageType);

            if (typeId != 0)
                Apply(mapChannel, target, source, typeId, CritStunMs, damageType);
        }
    }
}
