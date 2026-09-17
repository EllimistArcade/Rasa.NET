using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// Buffs and debuffs on actors. An effect has a type (the client's gameeffectdata id, which
    /// picks the client class and visuals), an instance id, a level, and whatever the server
    /// needs to apply it: when it wears off, what it does on a tick, how it changes movement.
    ///
    /// Timing is by Environment.TickCount64. The worker runs on a 500 ms timer but was handed
    /// the world-loop delta, so an effect aged by one loop tick every half second and a "5
    /// second" sprint ran for the better part of a minute.
    /// </summary>
    public class GameEffectManager
    {
        private static GameEffectManager _instance;
        private static readonly object InstanceLock = new object();

        /// <summary>gameeffectdata.SPRINT.</summary>
        public const int SprintTypeId = 247;

        public static GameEffectManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new GameEffectManager();
                    }
                }

                return _instance;
            }
        }

        private GameEffectManager()
        {
        }

        public void AddToList(Actor actor, GameEffect gameEffect)
        {
            actor.ActiveEffects[gameEffect.EffectId] = gameEffect;
        }

        public void RemoveFromList(Actor actor, GameEffect gameEffect)
        {
            actor.ActiveEffects.Remove(gameEffect.EffectId);
        }

        /// <summary>Allocates the next effect id on the map. Effect ids are per map, like entity cells.</summary>
        public int NextEffectId(MapChannel mapChannel)
        {
            mapChannel.CurrentEffectId++;
            return mapChannel.CurrentEffectId;
        }

        /// <summary>
        /// Registers an effect on an actor and tells everyone who can see them. attachArgs are
        /// passed to the client effect's OnAttach; the sprint effect wants its bead modifier
        /// there, most effects want nothing.
        /// </summary>
        public void Attach(MapChannel mapChannel, Actor actor, GameEffect effect, bool isBuff, params object[] attachArgs)
        {
            AddToList(actor, effect);

            CellManager.Instance.CellCallMethod(mapChannel, actor, new GameEffectAttachedPacket
            {
                EffectTypeId = effect.TypeId,
                EffectId = effect.EffectId,
                EffectLevel = effect.EffectLevel,
                SourceId = effect.SourceId,
                Announced = true,
                Duration = effect.RemainingSeconds,
                DamageType = 0,
                AttrId = 1,
                IsActive = true,
                IsBuff = isBuff,
                IsDebuff = !isBuff,
                IsNegativeEffect = !isBuff,
                Args = attachArgs.ToList()
            });

            if (effect.MovementModifierPercent != 0)
                UpdateMovementMod(mapChannel, actor);
        }

        /// <summary>
        /// Sprint, from its action_property row: EFFECT_MOVEMENT_MODIFIER is the speed as a
        /// percent (120 at level 1, 160 at 5), DURATION the cap in seconds, and
        /// DRAIN_PER_TICK_ADRENALINE over INTERVAL the adrenaline it burns. The client's tooltip
        /// shows that as drain / (interval * 10) and calls it "-1.5% every second"
        /// (abilities/sprint.py, uielement 2578): a percent of the adrenaline bar, so the same
        /// pump costs the same share of the bar at every level. This takes the same. It ends when
        /// the duration is up, the bar is empty, the player right-clicks the buff away (a detach
        /// request), or the player presses sprint again - AbilityManager turns a second request
        /// into ending the first. There is no cost to start or stop it; the drain is the cost.
        /// </summary>
        public GameEffect AttachSprint(MapChannel mapChannel, Actor actor, ActionLevelInfo level)
        {
            foreach (var existing in actor.ActiveEffects.Values.Where(e => e.TypeId == SprintTypeId).ToList())
                DettachEffect(mapChannel, actor, existing);

            var durationSeconds = level.Get(AbilityProperty.Duration, 3600);
            var interval = Math.Max(1, level.Get(AbilityProperty.Interval, 2));
            var drainPerTick = level.Get(AbilityProperty.DrainPerTickAdrenaline, 0);
            var now = Environment.TickCount64;

            var effect = new GameEffect
            {
                TypeId = SprintTypeId,
                EffectId = NextEffectId(mapChannel),
                EffectLevel = level.Level,
                ActionId = level.ActionId,
                SourceId = actor.EntityId,
                ExpiresTick = now + durationSeconds * 1000L,
                TickIntervalMs = 1000,
                NextTickTick = now + 1000,
                MovementModifierPercent = level.Get(AbilityProperty.EffectMovementModifier, 100),
                AdrenalineDrainPercentPerSecond = drainPerTick / (interval * 10.0),
                AllowDetach = true
            };

            // EFFECT_FAST_MAXBEAD_MODIFIER, as a fraction: the client's SprintEffect.OnAttach(beadModifier).
            var beadModifier = level.Get(AbilityProperty.EffectFastMaxbeadModifier, 100) / 100.0;

            Attach(mapChannel, actor, effect, true, beadModifier);

            return effect;
        }

        public void DettachEffect(MapChannel mapChannel, Actor actor, GameEffect gameEffect)
        {
            // inform clients (Recv_GameEffectDetached 75)
            CellManager.Instance.CellCallMethod(mapChannel, actor, new GameEffectDetachedPacket { EffectId = gameEffect.EffectId });
            RemoveFromList(actor, gameEffect);

            if (gameEffect.MovementModifierPercent != 0)
                UpdateMovementMod(mapChannel, actor);
        }

        /// <summary>
        /// Ends every effect on an actor without telling anyone: the actor is leaving the map.
        ///
        /// The speed they gave goes with them. MovementSpeed is what the effects make it (see
        /// <see cref="UpdateMovementMod"/>), and the ActorInfo a player is sent on arriving at a
        /// map hands it to their client as its movement modifier. Clearing the effects but not
        /// the speed sent a player who crossed a zone line mid-sprint into the next map running at
        /// sprint speed with no sprint behind it: nothing drained adrenaline, nothing ran out, and
        /// no buff was there to turn off, until a later sprint ended and worked the speed out again.
        /// </summary>
        public void ClearEffects(Actor actor)
        {
            actor.ActiveEffects.Clear();
            actor.MovementSpeed = 1.0d;
        }

        public void DoWork(MapChannel mapChannel, long passedTime)
        {
            foreach (var client in mapChannel.ClientList)
            {
                var actor = client?.Player;

                if (actor == null || actor.ActiveEffects.Count == 0)
                    continue;

                // The list changes under a detach, so work from a copy.
                foreach (var effect in actor.ActiveEffects.Values.ToList())
                {
                    if (effect.IsExpired)
                    {
                        DettachEffect(mapChannel, actor, effect);
                        continue;
                    }

                    if (effect.TickDue)
                        Tick(mapChannel, client, actor, effect);
                }
            }
        }

        /// <summary>One scheduled tick of an effect. Drains are the only tick today; damage-over-time joins here.</summary>
        private void Tick(MapChannel mapChannel, Client client, Actor actor, GameEffect effect)
        {
            effect.NextTickTick += effect.TickIntervalMs;

            if (effect.AdrenalineDrainPercentPerSecond > 0 && actor.Attributes.TryGetValue(Attributes.Chi, out var chi))
            {
                var take = DrainThisTick(effect, chi.CurrentMax);

                if (take <= 0)
                    return;

                // "will end once all Adrenaline has been consumed": the last tick takes what is
                // left and the effect goes with it.
                var ended = chi.Current <= take;

                chi.Current = Math.Max(0, chi.Current - take);
                client.CallMethod(actor.EntityId, new UpdateChiPacket(chi, 0));

                if (ended)
                    DettachEffect(mapChannel, actor, effect);
            }
        }

        /// <summary>
        /// The whole points of adrenaline one tick of a drain takes, out of a bar of the given
        /// maximum: a percent of the bar per second, with the fraction carried on the effect so
        /// 1.5% of 100 takes 3 every two seconds - 1, then 2 - rather than rounding down to 1
        /// every second and running at two thirds of the advertised rate.
        /// </summary>
        public static int DrainThisTick(GameEffect effect, int currentMax)
        {
            var due = currentMax * effect.AdrenalineDrainPercentPerSecond / 100.0 * effect.TickIntervalMs / 1000.0 + effect.DrainCarry;

            // Twenty carries of 0.3 sum to 5.999..., not 6; the nudge keeps a whole point that
            // floating-point arithmetic has left a hair short from slipping a tick.
            var take = (int)Math.Floor(due + 1e-6);

            effect.DrainCarry = due - take;

            return take;
        }

        /// <summary>
        /// Movement speed from the effects on an actor, as the client's Recv_MovementModChange
        /// wants it: 1.0 is normal. Modifiers multiply, so two 120s make 1.44.
        /// </summary>
        public void UpdateMovementMod(MapChannel mapChannel, Actor actor)
        {
            var movementMod = 1.0d;

            foreach (var effect in actor.ActiveEffects.Values)
                if (effect.MovementModifierPercent > 0)
                    movementMod *= effect.MovementModifierPercent / 100.0;

            // ActorInfo carries MovementSpeed later - to whoever a creature comes into view for, and
            // to a player's own client each time they arrive on a map - so it has to say the same
            // thing as the change everyone present is told about now.
            actor.MovementSpeed = movementMod;
            CellManager.Instance.CellCallMethod(mapChannel, actor, new MovementModChangePacket(movementMod));
        }
    }
}
