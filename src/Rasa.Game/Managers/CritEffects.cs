using System;

namespace Rasa.Managers
{
    using Data;
    using Structures;

    /// <summary>
    /// What a player's critical hit does to a creature besides the extra damage, by the hit's
    /// damage type - the classes in the client's gameeffects/criteffects.py:
    ///
    ///  - Ice: CRIT_ICE 3, a StunEffect, "Frozen" (Stuns).
    ///  - Sonic: CRIT_SONIC 7, a KnockbackEffect, "Stunned" (CrowdControl).
    ///  - Virulent: CRIT_VIRULENT 10000018, "Crippled: %(snareMod)s%% Movement" (CrowdControl).
    ///  - Fire: CRIT_FIRE 2, a DamageOverTime, "Fire Damage: %(dmgAmt)s every %(interval)s sec." -
    ///    FireDotPercent of the crit's damage as fire, every FireDotIntervalMs for FireDotMs.
    ///  - EMP: CRIT_EMP 417, "Armor Suppression" - for EmpSuppressMs the creature's armour stops
    ///    nothing: every hit goes straight to its health (GameEffect.SuppressesArmor).
    ///  - Laser: CRIT_LIGHT 6, "Ranged Damage: %(dmgMod)s%%" - the creature's ranged attacks do
    ///    LaserRangedDamagePercent less for LaserMs.
    ///
    /// The client has the effects and their tooltips but none of the numbers; every figure
    /// below is chosen here.
    /// </summary>
    public static class CritEffects
    {
        public const int CritFireTypeId = 2;        // CRIT_FIRE
        public const int CritLightTypeId = 6;       // CRIT_LIGHT
        public const int CritEmpTypeId = 417;       // CRIT_EMP

        public const int FireDotPercent = 10;
        public const int FireDotIntervalMs = 1000;
        public const int FireDotMs = 5000;

        public const int EmpSuppressMs = 5000;

        public const int LaserRangedDamagePercent = 25;
        public const int LaserMs = 6000;

        /// <summary>A Fire crit's burn per tick: FireDotPercent of the crit's damage, at least 1.</summary>
        public static int FireTick(int critDamage) => Math.Max(1, critDamage * FireDotPercent / 100);

        /// <summary>
        /// A critical hit's side effect on a creature. <paramref name="damage"/> is what the crit
        /// did, for the Fire burn.
        /// </summary>
        public static void OnCritical(MapChannel mapChannel, Creature target, Actor source, DamageType damageType, int damage)
        {
            if (target == null || source == null || target.State == CharacterState.Dead || target.State == CharacterState.Dying)
                return;

            switch (damageType)
            {
                case DamageType.Ice:
                    Stuns.Apply(mapChannel, target, source, Stuns.CritIceTypeId, Stuns.CritStunMs, damageType);
                    break;
                case DamageType.Sonic:
                    CrowdControl.Knockback(mapChannel, target, source, CrowdControl.DefaultKnockbackDistance, CrowdControl.CritSonicTypeId, damageType);
                    break;
                case DamageType.Virulent:
                    CrowdControl.Slow(mapChannel, target, source, CrowdControl.CritVirulentTypeId, CrowdControl.VirulentCrippleSlowPercent, CrowdControl.VirulentCrippleMs, "snareMod");
                    break;
                case DamageType.Fire:
                    Burn(mapChannel, target, source, damage);
                    break;
                case DamageType.EMP:
                    SuppressArmor(mapChannel, target, source);
                    break;
                case DamageType.Laser:
                    WeakenRanged(mapChannel, target, source);
                    break;
            }
        }

        private static GameEffect NewDebuff(MapChannel mapChannel, Actor source, int typeId, int durationMs)
        {
            return new GameEffect
            {
                TypeId = typeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = 1,
                SourceId = source.EntityId,
                Source = source,
                SourceLevel = (source as Manifestation)?.Level ?? 1,
                IsBuff = false,
                ExpiresTick = Environment.TickCount64 + durationMs
            };
        }

        /// <summary>
        /// Fire: a burn of FireTick(damage) fire damage every interval, the first an interval in.
        /// Not scaled again - it is a share of damage already scaled - and not a crit itself. A
        /// second Fire crit replaces the burn with a fresh one.
        /// </summary>
        public static void Burn(MapChannel mapChannel, Creature target, Actor source, int damage)
        {
            var tick = FireTick(damage);
            var burn = NewDebuff(mapChannel, source, CritFireTypeId, FireDotMs);

            burn.TickDamageMin = tick;
            burn.TickDamageMax = tick;
            burn.TickDamageType = DamageType.Fire;
            burn.TickScaleType = 0;
            burn.TickIntervalMs = FireDotIntervalMs;
            burn.NextTickTick = Environment.TickCount64 + FireDotIntervalMs;
            burn.Tooltip["dmgAmt"] = tick;
            burn.Tooltip["interval"] = FireDotIntervalMs / 1000;

            GameEffectManager.Instance.Attach(mapChannel, target, burn);
        }

        /// <summary>EMP: for EmpSuppressMs every hit on the creature ignores its armour.</summary>
        public static void SuppressArmor(MapChannel mapChannel, Creature target, Actor source)
        {
            var suppression = NewDebuff(mapChannel, source, CritEmpTypeId, EmpSuppressMs);

            suppression.SuppressesArmor = true;

            GameEffectManager.Instance.Attach(mapChannel, target, suppression);
        }

        /// <summary>Laser: the creature's ranged attacks do LaserRangedDamagePercent less for LaserMs.</summary>
        public static void WeakenRanged(MapChannel mapChannel, Creature target, Actor source)
        {
            var weakened = NewDebuff(mapChannel, source, CritLightTypeId, LaserMs);

            weakened.RangedDamagePercent = -LaserRangedDamagePercent;
            weakened.Tooltip["dmgMod"] = -LaserRangedDamagePercent;

            GameEffectManager.Instance.Attach(mapChannel, target, weakened);
        }
    }
}
