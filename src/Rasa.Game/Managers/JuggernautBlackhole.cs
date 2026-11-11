using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// A Juggernaut's vortex: CR_JUGGERNAUT_BLACKHOLE 493 (JuggernautBlackholeAbility, "Causes a
    /// Juggernaut to summon a Blackhole"). From the client and its data:
    /// - the Juggernaut winds up (1.2 s, its eye beam's windup, 1433) and recovers (2.8 s, 958)
    ///   with the blackhole opening, at 60 m at most, a new one every 12 s at most;
    /// - the blackhole is a creature of class Ability_Bane_Juggernaught_Blackhole (28687) that
    ///   stands for the argument's EFFECT_DURATION_MS (6 s);
    /// - what it does is CR_BLACKHOLE_GRAVITY_WELL 494 (BlackholeGravityWellAbility): every
    ///   non-friendly within EFFECT_RADIUS (15 m) gets BLACKHOLE_GRAVITY_WELL_TARGET 385, its
    ///   targetGameEffect - a DamageOverTime whose OnAttach(bRootTarget, bTeleportTarget,
    ///   teleportDuration) flails the player while they are dragged in, stands them up after
    ///   teleportDuration, and draws the effect's FX family to its source - burning them every
    ///   EFFECT_INTERVAL_MS (1 s) for EFFECT_DURATION_MS (5 s), in the argument's DAMAGE_TYPE.
    ///
    /// The server's part, as the Reality Ripper's rift (AbilityManager.RealityRipper):
    /// - the blackhole is a scripted creature of the Juggernaut's side where its target stood,
    ///   so a player can shoot it, and destroying it closes it - nobody's kill or loot;
    /// - every EFFECT_INTERVAL_MS each player within the radius it may fight and reaches
    ///   (CreatureBombs.Caught), not already taken, is dragged to CrowdControl.PullStopShort of
    ///   it over TeleportMs (PlayerCrowdControl.Pull) and given 385 for the well's time, or what
    ///   is left of the blackhole's: the burn is the Juggernaut's blackhole row's damage, a tick;
    /// - when its time is up, or it is destroyed, or its Juggernaut dies, the burns come off and
    ///   it is taken away.
    ///
    /// Not in the client, so chosen: TeleportMs as the Reality Ripper's, the blackhole's health
    /// HealthPercent of its Juggernaut's, and one blackhole open per Juggernaut.
    /// </summary>
    public static class JuggernautBlackhole
    {
        public const ActionId Summon = (ActionId)493;                       // CR_JUGGERNAUT_BLACKHOLE
        public const ActionId GravityWell = (ActionId)494;                  // CR_BLACKHOLE_GRAVITY_WELL
        public const uint GravityWellArg = 1;
        public const int WellTargetTypeId = 385;                            // BLACKHOLE_GRAVITY_WELL_TARGET
        public const EntityClasses BlackholeClass = (EntityClasses)28687;   // Ability_Bane_Juggernaught_Blackhole

        /// <summary>Ours: how long the drag in takes, the Reality Ripper's.</summary>
        public const int TeleportMs = AbilityManager.RipperTeleportMs;

        /// <summary>Ours: the blackhole's health, as a share of its Juggernaut's.</summary>
        public const int HealthPercent = 10;

        /// <summary>The argument's numbers, where AbilityManager has none: 6 s open; 15 m, 1 s, 5 s for the well.</summary>
        public const int DefaultOpenMs = 6000, DefaultRadius = 15, DefaultIntervalMs = 1000, DefaultWellMs = 5000;

        private sealed class Hole
        {
            public MapChannel MapChannel;
            public Creature Creature;
            public Creature Juggernaut;
            public CreatureAction Action;
            public float Radius;
            public int IntervalMs;
            public int WellMs;
            public DamageType DamageType;
            public long ExpiresAt;
            public long NextPullAt;
            public readonly Dictionary<Manifestation, GameEffect> Taken = new Dictionary<Manifestation, GameEffect>();
        }

        private static readonly List<Hole> Holes = new List<Hole>();
        private static readonly object HolesLock = new object();

        public static bool Is(CreatureAction action) => action != null && action.ActionId == Summon;

        /// <summary>Whether this creature is a Juggernaut's blackhole.</summary>
        public static bool Is(Creature creature)
        {
            lock (HolesLock)
                return Holes.Any(h => h.Creature == creature);
        }

        /// <summary>The blackhole a Juggernaut has open, if it has one.</summary>
        public static Creature OpenBy(Creature juggernaut)
        {
            lock (HolesLock)
                return Holes.FirstOrDefault(h => h.Juggernaut == juggernaut)?.Creature;
        }

        private static bool Alive(Actor actor) =>
            actor != null && actor.State != CharacterState.Dead && actor.State != CharacterState.Dying
            && actor.Attributes.TryGetValue(Attributes.Health, out var health) && health.Current > 0;

        /// <summary>
        /// A Juggernaut in a fight with a player opens a blackhole at them, if it has none open:
        /// the windup now, the blackhole when it is done (CreatureWindups.After). Whether it did.
        /// </summary>
        public static bool Perform(MapChannel mapChannel, Creature juggernaut, CreatureAction action, Actor target)
        {
            if (mapChannel == null || juggernaut == null || !Is(action) || !(target is Manifestation) || !Alive(target)
                || AbilityManager.Instance == null || !AbilityManager.Instance.TryGetLevel(action.ActionId, action.ActionArgId, out var info))
                return false;

            if (OpenBy(juggernaut) != null)
                return false;

            CellManager.Instance.CellCallMethod(mapChannel, juggernaut,
                new PerformWindupPacket(PerformType.TwoArgs, action.ActionId, action.ActionArgId));

            CreatureWindups.After(mapChannel, juggernaut, CreatureWindups.WindupMsOf(action, info),
                () => Open(mapChannel, juggernaut, action, target), action);

            return true;
        }

        /// <summary>The windup is done: the recovery, and the blackhole where its target stands. Null when it came to nothing.</summary>
        internal static Creature Open(MapChannel mapChannel, Creature juggernaut, CreatureAction action, Actor target)
        {
            if (!Alive(juggernaut) || !Alive(target) || target.MapContextId != juggernaut.MapContextId || OpenBy(juggernaut) != null)
            {
                CreatureWindups.Interrupt(mapChannel, juggernaut, action.ActionId, action.ActionArgId);
                return null;
            }

            AbilityManager.Instance.TryGetLevel(action.ActionId, action.ActionArgId, out var info);
            AbilityManager.Instance.TryGetLevel(GravityWell, GravityWellArg, out var well);

            CellManager.Instance.CellCallMethod(mapChannel, juggernaut,
                new AbilityRecoveryPacket(action.ActionId, action.ActionArgId, AbilityRecoveryPacket.HitDataKind.None));

            var spot = NavMeshManager.SnapToGround(mapChannel, target.Position);
            var health = Math.Max(1, (int)(juggernaut.Attributes[Attributes.Health].CurrentMax * (long)HealthPercent / 100));
            var hole = new Creature
            {
                EntityClass = BlackholeClass,
                TargetCategory = juggernaut.TargetCategory,
                Level = juggernaut.Level,
                MaxHitPoints = (uint)health,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>(),
                State = CharacterState.Idle,
                IsScripted = true,
                MasterEntityId = juggernaut.EntityId,
                Name = "Blackhole"
            };

            hole.Attributes.Add(Attributes.Body, new ActorAttributes(Attributes.Body, 1, 1, 1, 0, 0));
            hole.Attributes.Add(Attributes.Mind, new ActorAttributes(Attributes.Mind, 1, 1, 1, 0, 0));
            hole.Attributes.Add(Attributes.Spirit, new ActorAttributes(Attributes.Spirit, 1, 1, 1, 0, 0));
            hole.Attributes.Add(Attributes.Health, new ActorAttributes(Attributes.Health, health, health, health, 0, 0));
            hole.Attributes.Add(Attributes.Chi, new ActorAttributes(Attributes.Chi, 0, 0, 0, 0, 0));
            hole.Attributes.Add(Attributes.Power, new ActorAttributes(Attributes.Power, 0, 0, 0, 0, 0));
            hole.Attributes.Add(Attributes.Aware, new ActorAttributes(Attributes.Aware, 0, 0, 0, 0, 0));
            hole.Attributes.Add(Attributes.Armor, new ActorAttributes(Attributes.Armor, 0, 0, 0, 0, 0));
            hole.Attributes.Add(Attributes.Speed, new ActorAttributes(Attributes.Speed, 1, 1, 1, 0, 0));
            hole.Attributes.Add(Attributes.Regen, new ActorAttributes(Attributes.Regen, 0, 0, 0, 0, 0));

            CreatureManager.Instance.SetLocation(hole, spot, juggernaut.Rotation, juggernaut.MapContextId);
            CellManager.Instance.AddToWorld(mapChannel, hole);

            var now = Environment.TickCount64;

            lock (HolesLock)
                Holes.Add(new Hole
                {
                    MapChannel = mapChannel,
                    Creature = hole,
                    Juggernaut = juggernaut,
                    Action = action,
                    Radius = Math.Max(1, well?.Get(AbilityProperty.EffectRadius, DefaultRadius) ?? DefaultRadius),
                    IntervalMs = Math.Max(250, well?.Get(AbilityProperty.EffectIntervalMs, DefaultIntervalMs) ?? DefaultIntervalMs),
                    WellMs = Math.Max(250, well?.Get(AbilityProperty.EffectDurationMs, DefaultWellMs) ?? DefaultWellMs),
                    DamageType = (DamageType)(well?.Get(AbilityProperty.DamageType, (int)DamageType.Physical) ?? (int)DamageType.Physical),
                    ExpiresAt = now + Math.Max(1000, info?.Get(AbilityProperty.EffectDurationMs, DefaultOpenMs) ?? DefaultOpenMs),
                    NextPullAt = now
                });

            return hole;
        }

        /// <summary>Runs the blackholes on this map: take in whoever is in reach, close the spent ones. Run every map tick.</summary>
        public static void Worker(MapChannel mapChannel) => Worker(mapChannel, Environment.TickCount64);

        internal static void Worker(MapChannel mapChannel, long now)
        {
            List<Hole> holes;

            lock (HolesLock)
                holes = Holes.Where(h => h.MapChannel == mapChannel).ToList();

            foreach (var hole in holes)
            {
                if (now >= hole.ExpiresAt || !Alive(hole.Creature) || !Alive(hole.Juggernaut)
                    || hole.Juggernaut.MapContextId != hole.Creature.MapContextId)
                {
                    Close(hole);
                    continue;
                }

                if (now < hole.NextPullAt)
                    continue;

                hole.NextPullAt = now + hole.IntervalMs;

                foreach (var gone in hole.Taken.Where(t => !Alive(t.Key) || !t.Key.ActiveEffects.ContainsKey(t.Value.EffectId)).Select(t => t.Key).ToList())
                    hole.Taken.Remove(gone);

                foreach (var player in CreatureBombs.Caught(mapChannel, hole.Juggernaut, hole.Creature.Position, hole.Radius))
                    if (!hole.Taken.ContainsKey(player) && !Pvp.IsSafe(player))
                        Take(hole, player, now);
            }
        }

        /// <summary>Drags a player in and burns them, for the well's time or what is left of the blackhole's.</summary>
        private static void Take(Hole hole, Manifestation player, long now)
        {
            var mapChannel = hole.MapChannel;
            var lasts = Math.Min(hole.WellMs, hole.ExpiresAt - now);

            if (lasts <= 0)
                return;

            PlayerCrowdControl.Pull(mapChannel, player, hole.Creature, TeleportMs);

            var min = (int)hole.Action.MinDamage;
            var burn = new GameEffect
            {
                TypeId = WellTargetTypeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = 1,
                ActionId = GravityWell,
                // The client's effect draws its FX to its source, so that is the blackhole; the
                // damage and the kill are the Juggernaut's.
                SourceId = hole.Creature.EntityId,
                Source = hole.Juggernaut,
                SourceLevel = (int)hole.Juggernaut.Level,
                IsBuff = false,
                AnnounceOnAttach = true,
                AllowDetach = false,
                ExpiresTick = now + lasts + 250,
                TickIntervalMs = hole.IntervalMs,
                NextTickTick = now + hole.IntervalMs,
                TickDamageMin = min,
                TickDamageMax = Math.Max(min, (int)hole.Action.MaxDamage),
                TickScaleType = 0,      // the row's numbers are already the creature's
                TickDamageType = hole.DamageType
            };

            // BlackholeGravityWellTargetEffect.OnAttach(target, bRootTarget, bTeleportTarget, teleportDuration).
            GameEffectManager.Instance.Attach(mapChannel, player, burn, 1, 1, TeleportMs);

            if (player.ActiveEffects.ContainsKey(burn.EffectId))
                hole.Taken[player] = burn;
        }

        /// <summary>A blackhole destroyed: it closes now, and is nobody's kill.</summary>
        public static void Killed(MapChannel mapChannel, Creature creature)
        {
            Hole hole;

            lock (HolesLock)
                hole = Holes.FirstOrDefault(h => h.Creature == creature);

            if (hole != null)
                Close(hole);
        }

        /// <summary>Takes the burns off and the blackhole out of the world.</summary>
        private static void Close(Hole hole)
        {
            lock (HolesLock)
                if (!Holes.Remove(hole))
                    return;

            var mapChannel = hole.MapChannel;
            var creature = hole.Creature;

            foreach (var (player, burn) in hole.Taken)
                if (player.ActiveEffects.ContainsKey(burn.EffectId))
                    GameEffectManager.Instance.DettachEffect(mapChannel, player, burn);

            creature.State = CharacterState.Dead;
            creature.Attributes[Attributes.Health].Current = 0;

            CellManager.Instance.RemoveCreatureFromWorld(mapChannel, creature);
        }
    }
}
