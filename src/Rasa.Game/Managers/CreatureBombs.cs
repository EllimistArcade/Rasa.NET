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
    /// The creature attacks that explode, from their client classes:
    ///
    ///  - Death blast (WardenBotDeathAbility, CR_WARDEN_BOT_DEATH 480): when the Warden dies, a
    ///    recovery of the action listing every player within RADIUS_AROUND_SOURCE with its damage,
    ///    as the bare rawInfo the class's DoAbility reads - the shape of a crab mine's
    ///    CR_CRAB_MINE_DEATH, which the class is a copy of.
    ///  - Death bomb (HowlerDeathAbility 514, PredatorDeathAbility 407): a BombEffect on the dying
    ///    creature - HOWLER_DEATH, PREDATOR_DEATH_EXPLOSION - that goes off DELAY_TIME_MS later
    ///    (the Howler at once, the Predator 3 s on): CallGameEffectMethod DoExplosion(damageData),
    ///    which plays the blast where the creature lies and floats every (entityId, rawInfo) in it.
    ///    The Predator's class names the effect as its targetGameEffect on TARGET_SELF, so a
    ///    recovery listing the Predator itself announces it; the Howler's names none, so its
    ///    effect is announced as it attaches. HowlerDeathEffect has removeTarget: the client takes
    ///    the Howler away when it goes off - there is no corpse to loot.
    ///  - Self-destruct (FithikSelfDestructAbility 180): a Fithik brought down to
    ///    SelfDestructHealthPercent of its health stops, winds up (4.3 s, the client's own
    ///    animation), and blows up: a recovery of the action with every player within
    ///    RADIUS_AROUND_SOURCE, bare rawInfo, and the Fithik dies of it, the kill its target's. One
    ///    killed during the windup does not go off. When to do it is ours: the data has the blast,
    ///    not the reason.
    ///  - Ground blast (LinkerGroundBlastAbility 264): a Linker's attack is a bomb on its target -
    ///    LINKER_GROUND_BLAST, the class's targetGameEffect, announced by the attack's recovery -
    ///    going off at once on everyone within EFFECT_RADIUS of that player
    ///    (CreatureEffectAttacks puts it on; the attack itself does no damage).
    ///  - Missile (PredatorMissileAbility 426): the same on the player a Predator's missile hits -
    ///    PREDATOR_MISSILE_EXPLOSION, a BombEffect whose removeTarget is off, the class's
    ///    targetGameEffect - going off at once within EFFECT_RADIUS (10 m).
    ///
    /// The damage is the creature_action row's, resisted as the argument's DAMAGE_TYPE (physical
    /// when it gives none), and only players take it. Death actions sit on the creature's row with
    /// a range of 0, which the fighting loop never uses; they are found on the creature's actions
    /// when it dies.
    /// </summary>
    public static class CreatureBombs
    {
        public enum Kind { None, DeathBlast, DeathBomb, SelfDestruct, GroundBlast }

        public const ActionId WardenBotDeath = (ActionId)480;
        public const ActionId HowlerDeath = (ActionId)514;
        public const ActionId PredatorDeath = (ActionId)407;
        public const ActionId FithikSelfDestruct = (ActionId)180;
        public const ActionId LinkerGroundBlast = (ActionId)264;
        public const ActionId PredatorMissile = (ActionId)426;

        public const int HowlerDeathTypeId = 461;           // HOWLER_DEATH

        /// <summary>
        /// The level HOWLER_DEATH is attached at. BombEffect.Recv_DoExplosion plays the blast from
        /// gameeffectdata.specialFX[(typeId, level)], and the Howler's has one entry, at 13.
        /// </summary>
        public const int HowlerDeathFxLevel = 13;
        public const int PredatorDeathTypeId = 286;         // PREDATOR_DEATH_EXPLOSION
        public const int LinkerGroundBlastTypeId = 298;     // LINKER_GROUND_BLAST
        public const int PredatorMissileTypeId = 284;       // PREDATOR_MISSILE_EXPLOSION

        /// <summary>Ours: the share of its health at which a Fithik starts its self-destruct.</summary>
        public const int SelfDestructHealthPercent = 20;

        /// <summary>The blast's radius when the argument gives none.</summary>
        public const float DefaultRadius = 10f;

        public static Kind KindOf(ActionId actionId)
        {
            switch (actionId)
            {
                case WardenBotDeath: return Kind.DeathBlast;
                case HowlerDeath:
                case PredatorDeath: return Kind.DeathBomb;
                case FithikSelfDestruct: return Kind.SelfDestruct;
                case LinkerGroundBlast:
                case PredatorMissile: return Kind.GroundBlast;
                default: return Kind.None;
            }
        }

        public static bool IsDeathAction(CreatureAction action) => action != null && (KindOf(action.ActionId) == Kind.DeathBlast || KindOf(action.ActionId) == Kind.DeathBomb);

        public static bool IsSelfDestruct(CreatureAction action) => action != null && action.ActionId == FithikSelfDestruct;

        /// <summary>Whether a Fithik at this health should start its self-destruct.</summary>
        public static bool ShouldSelfDestruct(int health, int maxHealth) => maxHealth > 0 && health > 0 && health * 100 <= maxHealth * SelfDestructHealthPercent;

        /// <summary>The blast's radius from the argument: RADIUS_AROUND_SOURCE, else EFFECT_RADIUS, else DefaultRadius.</summary>
        public static float RadiusOf(ActionLevelInfo info)
        {
            if (info == null)
                return DefaultRadius;

            if (info.Get(AbilityProperty.RadiusAroundSource) > 0)
                return info.Get(AbilityProperty.RadiusAroundSource);

            return info.Get(AbilityProperty.EffectRadius) > 0 ? info.Get(AbilityProperty.EffectRadius) : DefaultRadius;
        }

        private sealed class Pending
        {
            public MapChannel MapChannel;
            public Creature Source;
            public Actor Holder;
            public CreatureAction Action;
            public GameEffect Effect;          // null for a self-destruct, which is a recovery
            public float Radius;
            public DamageType DamageType;
            public long GoesOffAt;
        }

        private static readonly List<Pending> Bombs = new List<Pending>();
        private static readonly object BombsLock = new object();
        private static readonly Random Random = new Random();

        /// <summary>Whether the creature is winding up its self-destruct: it does nothing else.</summary>
        public static bool IsSelfDestructing(Creature creature)
        {
            lock (BombsLock)
                return Bombs.Any(b => b.Source == creature && b.Effect == null);
        }

        private static ActionLevelInfo LevelOf(CreatureAction action)
        {
            if (AbilityManager.Instance == null || !AbilityManager.Instance.TryGetLevel(action.ActionId, action.ActionArgId, out var info))
                return null;

            return info;
        }

        private static DamageType TypeOf(ActionLevelInfo info) => (DamageType)(info?.Get(AbilityProperty.DamageType, (int)DamageType.Physical) ?? (int)DamageType.Physical);

        /// <summary>A creature has just died: its death action, if it has one, goes off.</summary>
        public static void OnDeath(MapChannel mapChannel, Creature creature)
        {
            if (mapChannel == null || creature?.Actions == null)
                return;

            // A self-destruct cut short by a kill does not go off.
            lock (BombsLock)
                Bombs.RemoveAll(b => b.Source == creature && b.Effect == null);

            var action = creature.Actions.FirstOrDefault(IsDeathAction);

            if (action == null)
                return;

            var info = LevelOf(action);

            if (info == null)
                return;

            if (KindOf(action.ActionId) == Kind.DeathBlast)
            {
                Blast(mapChannel, creature, action, RadiusOf(info), TypeOf(info));
                return;
            }

            var predator = action.ActionId == PredatorDeath;
            var delayMs = Math.Max(0, info.Get(AbilityProperty.DelayTimeMs));
            var bomb = NewBomb(mapChannel, creature, creature, info, predator ? PredatorDeathTypeId : HowlerDeathTypeId, delayMs, announce: !predator);

            GameEffectManager.Instance.Attach(mapChannel, creature, bomb);

            // The Predator's class announces its bomb on the hits of the recovery; the Howler's
            // recovery is its death animation.
            var recovery = new AbilityRecoveryPacket(action.ActionId, action.ActionArgId, AbilityRecoveryPacket.HitDataKind.None);

            if (predator)
                recovery.Hits.Add(new AbilityHit { EntityId = creature.EntityId });

            CellManager.Instance.CellCallMethod(mapChannel, creature, recovery);

            Arm(mapChannel, creature, creature, action, bomb, RadiusOf(info), TypeOf(info), delayMs);
        }

        /// <summary>The bomb an attack that lands as one puts on the player it hits: a Predator's missile's, else a Linker's ground blast.</summary>
        public static int GroundBlastTypeOf(ActionId actionId) => actionId == PredatorMissile ? PredatorMissileTypeId : LinkerGroundBlastTypeId;

        /// <summary>A Linker's ground blast or a Predator's missile has hit a player: the bomb on them, going off at once.</summary>
        public static GameEffect GroundBlast(MapChannel mapChannel, Creature linker, Manifestation player, CreatureAction action, ActionLevelInfo info)
        {
            if (mapChannel == null || linker == null || player == null || action == null || info == null)
                return null;

            var bomb = NewBomb(mapChannel, linker, player, info, GroundBlastTypeOf(action.ActionId), 0, announce: false);

            bomb.IsBuff = false;
            GameEffectManager.Instance.Attach(mapChannel, player, bomb);

            if (!player.ActiveEffects.ContainsKey(bomb.EffectId))
                return null;    // turned away (Cure's guard)

            // The recovery that announces it goes out first; the blast follows on the next tick.
            Arm(mapChannel, linker, player, action, bomb, RadiusOf(info), TypeOf(info), 0);

            return bomb;
        }

        /// <summary>A Fithik starts its self-destruct: the windup to everyone who can see it, and the blast when it is up.</summary>
        public static void StartSelfDestruct(MapChannel mapChannel, Creature fithik, CreatureAction action)
        {
            if (mapChannel == null || fithik == null || action == null || IsSelfDestructing(fithik))
                return;

            var info = LevelOf(action);
            var windupMs = info?.WindupMs ?? 0;

            BehaviorManager.Instance.StopMoving(fithik);
            fithik.Controller.Path.Clear();

            CellManager.Instance.CellCallMethod(mapChannel, fithik, new PerformWindupPacket(PerformType.TwoArgs, action.ActionId, action.ActionArgId));

            Arm(mapChannel, fithik, fithik, action, null, RadiusOf(info), TypeOf(info), windupMs);
        }

        private static GameEffect NewBomb(MapChannel mapChannel, Creature source, Actor holder, ActionLevelInfo info, int typeId, long delayMs, bool announce)
        {
            return new GameEffect
            {
                TypeId = typeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = typeId == HowlerDeathTypeId ? HowlerDeathFxLevel : info.Level,
                ActionId = info.ActionId,
                SourceId = source.EntityId,
                Source = source,
                SourceLevel = (int)source.Level,
                IsBuff = true,
                ExpiresTick = Environment.TickCount64 + delayMs + 5000,     // a backstop; the blast takes it off
                AnnounceOnAttach = announce
            };
        }

        private static void Arm(MapChannel mapChannel, Creature source, Actor holder, CreatureAction action, GameEffect effect, float radius, DamageType type, long delayMs)
        {
            lock (BombsLock)
                Bombs.Add(new Pending
                {
                    MapChannel = mapChannel,
                    Source = source,
                    Holder = holder,
                    Action = action,
                    Effect = effect,
                    Radius = radius,
                    DamageType = type,
                    GoesOffAt = Environment.TickCount64 + delayMs
                });
        }

        /// <summary>Sets off the bombs on this map whose time has come. Run every map tick.</summary>
        public static void Worker(MapChannel mapChannel)
        {
            List<Pending> due;
            var now = Environment.TickCount64;

            lock (BombsLock)
            {
                due = Bombs.Where(b => b.MapChannel == mapChannel && now >= b.GoesOffAt).ToList();

                foreach (var bomb in due)
                    Bombs.Remove(bomb);
            }

            foreach (var bomb in due)
            {
                if (bomb.Effect == null)
                    SelfDestruct(mapChannel, bomb);
                else
                    Explode(mapChannel, bomb);
            }
        }

        /// <summary>The players a blast around a point reaches: alive, on the map, within radius, and ones the creature may fight.</summary>
        public static List<Manifestation> Caught(MapChannel mapChannel, Creature source, Vector3 centre, float radius)
        {
            return mapChannel.ClientList
                .Select(c => c?.Player)
                .Where(p => p != null && p.State != CharacterState.Dead && p.State != CharacterState.Dying
                    && p.MapContextId == mapChannel.MapInfo.MapContextId
                    && p.Attributes.TryGetValue(Attributes.Health, out var health) && health.Current > 0
                    && Vector3.DistanceSquared(p.Position, centre) <= radius * radius
                    && TargetCategories.MayFightPlayer(source.TargetCategory, p.CombatCategory))
                .ToList();
        }

        private static (int Amount, int Resisted, bool Crit) Roll(Creature source, Actor victim, CreatureAction action, DamageType type)
        {
            int rolled;

            lock (Random)
                rolled = Random.Next((int)action.MinDamage, (int)Math.Max(action.MinDamage, action.MaxDamage) + 1);

            var crit = CriticalHits.Resolve(source, victim, false, CriticalHits.AttackerChance(source, false), ref rolled);
            var amount = GameEffectManager.ApplyResist(victim, rolled, out var resisted, type);

            return (amount, resisted, crit);
        }

        /// <summary>A BombEffect goes off: damage to everyone caught, then DoExplosion on the holder to show it, and the effect ends.</summary>
        private static void Explode(MapChannel mapChannel, Pending bomb)
        {
            var holder = bomb.Holder;

            if (holder == null || holder.MapContextId != mapChannel.MapInfo.MapContextId || !holder.ActiveEffects.ContainsKey(bomb.Effect.EffectId))
                return;

            var blast = new GameEffectAnnounceDamagePacket(bomb.Effect.EffectId, "DoExplosion");

            foreach (var victim in Caught(mapChannel, bomb.Source, holder.Position, bomb.Radius))
            {
                var (amount, resisted, crit) = Roll(bomb.Source, victim, bomb.Action, bomb.DamageType);

                ActorManager.Instance.Damage(mapChannel, victim, amount, bomb.Source, bomb.DamageType);

                blast.Hits.Add(new TickEntry { EntityId = victim.EntityId, Amount = amount, Resisted = resisted, DamageType = bomb.DamageType, IsCritical = crit });
            }

            CellManager.Instance.CellCallMethod(mapChannel, holder, blast);
            GameEffectManager.Instance.DettachEffect(mapChannel, holder, bomb.Effect);
        }

        /// <summary>A death blast: the action's recovery on the dead creature, a hit on everyone around it.</summary>
        private static void Blast(MapChannel mapChannel, Creature source, CreatureAction action, float radius, DamageType type)
        {
            var recovery = new AbilityRecoveryPacket(action.ActionId, action.ActionArgId, AbilityRecoveryPacket.HitDataKind.RawInfo);

            foreach (var victim in Caught(mapChannel, source, source.Position, radius))
            {
                var (amount, resisted, crit) = Roll(source, victim, action, type);

                ActorManager.Instance.Damage(mapChannel, victim, amount, source, type);

                recovery.Hits.Add(new AbilityHit { EntityId = victim.EntityId, Amount = amount, Resisted = resisted, DamageType = type, IsCritical = crit });
            }

            CellManager.Instance.CellCallMethod(mapChannel, source, recovery);
        }

        /// <summary>The self-destruct's windup is up: the Fithik blows, and dies of it - the kill its target's.</summary>
        private static void SelfDestruct(MapChannel mapChannel, Pending bomb)
        {
            var fithik = bomb.Source;

            if (fithik.State == CharacterState.Dead || fithik.State == CharacterState.Dying || fithik.MapContextId != mapChannel.MapInfo.MapContextId
                || !fithik.Attributes.TryGetValue(Attributes.Health, out var health) || health.Current <= 0)
                return;

            var killer = EntityManager.Instance.GetActor(fithik.Controller.ActionFighting.TargetEntityId)
                ?? fithik.Hate.Ranked().Select(h => EntityManager.Instance.GetActor(h.Key)).FirstOrDefault(a => a != null);

            Blast(mapChannel, fithik, bomb.Action, bomb.Radius, bomb.DamageType);

            health.Current = 0;
            CellManager.Instance.CellCallMethod(mapChannel, fithik, new UpdateHealthPacket(health, fithik.EntityId));

            if (killer != null)
                CreatureManager.Instance.HandleCreatureKill(mapChannel, fithik, killer);
        }
    }
}
