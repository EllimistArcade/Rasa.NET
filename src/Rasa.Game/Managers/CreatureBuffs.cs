using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// The creature actions a creature performs on itself or its own side rather than at the
    /// player it is fighting:
    ///
    ///  - Rage (abilities.rage, CR_THRAX_RAGE 454): the player Soldier's Rage in a Thrax boss's
    ///    hands. RAGESOURCE 236 on the Thrax - the class's targetGameEffect, TARGET_SELF, so the
    ///    recovery listing the Thrax announces it - raising the damage it deals by
    ///    DAMAGE_PERCENT_MIN for DURATION, and as an aura every INTERVAL seconds RAGE 235 on the
    ///    Bane of its own side within RADIUS_AROUND_SOURCE, which RageSourceEffect.OnTick
    ///    announces on the ids each tick names. The argument's DAMAGE_PERCENT is 140 where the
    ///    player's is 30-50, read the same way: +140%.
    ///  - Scourge (abilities.scourge, CR_THRAX_SCOURGE 455): the Commando's Scourge.
    ///    SCOURGE_EFFECT 256 on the Thrax - the class's sourceGameEffect, announced by a recovery
    ///    with a hit - dealing the creature_action row's damage every second for DURATION to every
    ///    player within EFFECT_RADIUS, shown through the effect's AnnounceDamage as a player's is.
    ///  - Warcry (AttaWarcryAbility, CR_ATTA_HARVESTER_WARCRY 477): CALL_FOR_HELP_NUM_CREATURES
    ///    of its own side within RADIUS_AROUND_SOURCE (80 m, or as far as the cells around the
    ///    harvester reach, about 64 m) that are not already fighting join the harvester's fight,
    ///    nearest first. Once a fight (WarcryRearmMs): it is a call for help,
    ///    not a way to pull a whole field.
    ///
    /// Each is used only when it would do something - not while its effect is still on, not a
    /// warcry with nobody to hear it - and otherwise the fighting loop goes on to the next action.
    /// </summary>
    public static class CreatureBuffs
    {
        public const ActionId ThraxRage = (ActionId)454;
        public const ActionId ThraxScourge = (ActionId)455;
        public const ActionId HarvesterWarcry = (ActionId)477;

        public const int RageTypeId = 235;          // RAGE
        public const int RageSourceTypeId = 236;    // RAGESOURCE
        public const int ScourgeTypeId = 256;       // SCOURGE_EFFECT

        /// <summary>Ours: how long before a creature's warcry can call again.</summary>
        public const long WarcryRearmMs = 60000;

        private static readonly Dictionary<ulong, long> WarcryAt = new Dictionary<ulong, long>();
        private static readonly object WarcryLock = new object();

        public static bool Is(CreatureAction action) =>
            action != null && (action.ActionId == ThraxRage || action.ActionId == ThraxScourge || action.ActionId == HarvesterWarcry);

        /// <summary>Uses the action if it would do something now; whether it did.</summary>
        public static bool Perform(MapChannel mapChannel, Creature creature, CreatureAction action, Actor target)
        {
            if (mapChannel == null || creature == null || action == null || AbilityManager.Instance == null
                || !AbilityManager.Instance.TryGetLevel(action.ActionId, action.ActionArgId, out var info))
                return false;

            switch (action.ActionId)
            {
                case ThraxRage:
                    return Rage(mapChannel, creature, action, info);
                case ThraxScourge:
                    return Scourge(mapChannel, creature, action, info);
                case HarvesterWarcry:
                    return Warcry(mapChannel, creature, action, info, target);
                default:
                    return false;
            }
        }

        private static bool Has(Actor actor, int typeId) => actor.ActiveEffects.Values.Any(e => e.TypeId == typeId && !e.IsExpired);

        private static GameEffect NewEffect(MapChannel mapChannel, Creature creature, ActionLevelInfo info, int typeId, long durationMs)
        {
            return new GameEffect
            {
                TypeId = typeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = info.Level,
                ActionId = info.ActionId,
                SourceId = creature.EntityId,
                Source = creature,
                SourceLevel = (int)creature.Level,
                IsBuff = true,
                ExpiresTick = Environment.TickCount64 + durationMs,
                AnnounceOnAttach = false
            };
        }

        /// <summary>The windup and the recovery, which lists who the action reached.</summary>
        private static void Show(MapChannel mapChannel, Creature creature, CreatureAction action, IEnumerable<Actor> hits)
        {
            CellManager.Instance.CellCallMethod(mapChannel, creature,
                new PerformWindupPacket(PerformType.ThreeArgs, action.ActionId, action.ActionArgId, creature.EntityId));

            var recovery = new AbilityRecoveryPacket(action.ActionId, action.ActionArgId, AbilityRecoveryPacket.HitDataKind.None);

            foreach (var hit in hits)
                recovery.Hits.Add(new AbilityHit { EntityId = hit.EntityId });

            CellManager.Instance.CellCallMethod(mapChannel, creature, recovery);
        }

        private static bool Rage(MapChannel mapChannel, Creature creature, CreatureAction action, ActionLevelInfo info)
        {
            if (Has(creature, RageSourceTypeId))
                return false;

            var rage = NewEffect(mapChannel, creature, info, RageSourceTypeId, info.Get(AbilityProperty.Duration, 30) * 1000L);

            rage.DamageDealtPercent = info.Get(AbilityProperty.DamagePercentMin);
            rage.AllowDetach = true;
            rage.Tooltip["dmgMod"] = rage.DamageDealtPercent;
            rage.Tooltip["resistMod"] = 0;

            if (info.Get(AbilityProperty.RadiusAroundSource) > 0)
            {
                rage.AuraRadius = info.Get(AbilityProperty.RadiusAroundSource);
                rage.AuraChildTypeId = RageTypeId;
                rage.AuraTickAnnounces = true;
                rage.TickIntervalMs = Math.Max(1, info.Get(AbilityProperty.Interval, 5)) * 1000;
                rage.NextTickTick = Environment.TickCount64;
            }

            GameEffectManager.Instance.Attach(mapChannel, creature, rage);
            Show(mapChannel, creature, action, new[] { creature });

            return true;
        }

        private static bool Scourge(MapChannel mapChannel, Creature creature, CreatureAction action, ActionLevelInfo info)
        {
            if (Has(creature, ScourgeTypeId) || action.MaxDamage == 0)
                return false;

            var scourge = NewEffect(mapChannel, creature, info, ScourgeTypeId, info.Get(AbilityProperty.Duration, 15) * 1000L);

            scourge.TickRadius = info.Get(AbilityProperty.EffectRadius, 6);
            scourge.TickDamageMin = (int)action.MinDamage;
            scourge.TickDamageMax = (int)Math.Max(action.MinDamage, action.MaxDamage);
            scourge.TickDamageType = (DamageType)info.Get(AbilityProperty.DamageType, (int)DamageType.Physical);
            scourge.TickScaleType = 0;     // the row's numbers are already the creature's
            scourge.TickIntervalMs = 1000;
            scourge.NextTickTick = Environment.TickCount64 + 1000;

            GameEffectManager.Instance.Attach(mapChannel, creature, scourge);
            Show(mapChannel, creature, action, new[] { creature });

            return true;
        }

        private static bool Warcry(MapChannel mapChannel, Creature creature, CreatureAction action, ActionLevelInfo info, Actor target)
        {
            if (target == null)
                return false;

            var now = Environment.TickCount64;

            lock (WarcryLock)
                if (WarcryAt.TryGetValue(creature.EntityId, out var at) && now - at < WarcryRearmMs)
                    return false;

            var count = Math.Max(1, info.Get(AbilityProperty.CallForHelpNumCreatures, 1));
            var radius = info.Get(AbilityProperty.RadiusAroundSource, 20);
            var heard = AlliesWithin(mapChannel, creature, creature.Position, radius)
                .Where(a => a.Controller.CurrentAction != BehaviorManager.BehaviorActionFighting && !BehaviorManager.IsReturning(a))
                .OrderBy(a => Vector3.DistanceSquared(a.Position, creature.Position))
                .Take(count)
                .ToList();

            if (heard.Count == 0)
                return false;

            lock (WarcryLock)
                WarcryAt[creature.EntityId] = now;

            foreach (var ally in heard)
            {
                ally.Hate.Ensure(target.EntityId, 1);
                BehaviorManager.Instance.SetActionFighting(ally, target.EntityId);
            }

            Show(mapChannel, creature, action, heard);

            return true;
        }

        /// <summary>
        /// The creatures of the same side as this one within radius of a point: alive, on the map,
        /// not it. Looked for in the cells around the creature, as everything near an actor is.
        /// </summary>
        public static List<Creature> AlliesWithin(MapChannel mapChannel, Creature creature, Vector3 centre, float radius)
        {
            var all = new List<Creature>();

            foreach (var cell in CellManager.CellsIn(mapChannel, creature.Cells))
                all.AddRange(cell.CreatureList);

            return all
                .Distinct()
                .Where(c => c != null && c != creature && c.MapContextId == mapChannel.MapInfo.MapContextId
                    && c.TargetCategory == creature.TargetCategory
                    && c.State != CharacterState.Dead && c.State != CharacterState.Dying
                    && c.Attributes.TryGetValue(Attributes.Health, out var health) && health.Current > 0
                    && Vector3.DistanceSquared(c.Position, centre) <= radius * radius)
                .ToList();
        }
    }
}
