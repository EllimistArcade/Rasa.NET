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
    /// What a player's ability did as it landed that their own client shows only as it plays the
    /// recovery, for that client to be shown should it cancel the action before then.
    ///
    /// An ability's effect goes on with announce off and the recovery names it: the client's
    /// AddEffectAbility.DoAbility, and a damage ability's hit, call AnnounceGameEffectAttach for
    /// it - make it visible, post its status icon (PhysicalEntity.AnnounceGameEffectAttach). A
    /// creature's bars wait for the hit too: its health and armour updates come in the name of
    /// the creature itself, which the client holds, so it keeps them unannounced until a hit
    /// announces them (Actor.UpdateAttribute, AnnounceDamage's AnnounceAttrChange). That is the
    /// action's resolution, which waits for the strike or for the end of the recovery
    /// (BaseActorAction.DoAction, _resolutionMethod). A player who starts walking in that time
    /// interrupts it on their own side - a moveInterrupts action, AttemptInterruptAction - and
    /// Destroy throws the resolution away. Their RequestActionInterrupt reaches a server that has
    /// already landed the ability: the item used up, the cost paid, the effect on, the damage
    /// done. Nothing was going to show it them: an Experience Booster used, its boost working,
    /// and nothing on the player's bar to say so; a Polymorph on them they did not see; a Shrapnel
    /// whose targets' health bars stayed full.
    ///
    /// So while a player's recovery is resolved (<see cref="Open"/>), GameEffectManager.Attach
    /// tells <see cref="Note"/> of each effect it puts on quietly, ActorManager.Damage tells
    /// <see cref="NoteHit"/> of each creature or player the player's ability hits, and the last
    /// recovery's are kept on the player. An interrupt for that action that comes after it has
    /// landed (<see cref="Reannounce"/>) sends their client, on each of those still in the world,
    /// a hit of nothing: an AnnounceMapDamage whose rawInfo does no damage and names the types of
    /// the effects still on it in targetEffectIds. Actor.AnnounceDamage announces one of each type
    /// that has not been yet and announces its health and armour - what the recovery's hit would
    /// have done - and with nothing dealt it floats no number, writes no combat log line and
    /// plays no reaction (combatmessages.OnCombatDamage, AnnounceDamage). The numbers the hit
    /// would have floated are not shown: a hit with no source cannot be shown as the player's.
    /// Nobody else cancelled anything: the others play the recovery to its end.
    ///
    /// An effect the client had announced already - an action whose strike had fired before the
    /// player moved - is left as it is: AnnounceGameEffectAttach finds none of its type left to
    /// announce and only notes the type for the next one attached in ten seconds
    /// (PhysicalEntity.__delayedAnnounce). Taking it off and putting it back announced would have
    /// done the same for the one that had not been, and made the status icon of the one that had
    /// blink. Bars announced already are announced again at what they show.
    ///
    /// Only effects the caster is to be shown (GameEffect.AnnounceToCaster, as AnnounceToNewcomers
    /// unless set): one kept quiet for good, a vampiric drain's or a run speed module's, stays so;
    /// a risen corpse's REANIMATED, which a newcomer is not shown, the caster is.
    ///
    /// The client sends no interrupt once the action has finished - it is no longer its current
    /// one - so one that comes late is for an action it has cut short.
    /// </summary>
    public sealed class QuietLandings : IDisposable
    {
        /// <summary>How long after the landing an interrupt is still the one for it: the client's own wait for an announce (PhysicalEntity, 10 s).</summary>
        public const long WindowMs = 10_000;

        [ThreadStatic]
        private static QuietLandings _open;

        private readonly QuietLandings _outer;
        private readonly Manifestation _player;
        private readonly List<(Actor Holder, GameEffect Effect)> _effects = new List<(Actor, GameEffect)>();
        private readonly List<Actor> _hit = new List<Actor>();

        public ActionId ActionId { get; }
        public uint ActionArgId { get; }
        public long LandedTick { get; internal set; }

        /// <summary>The effects it put on quietly, and on whom.</summary>
        public IReadOnlyList<(Actor Holder, GameEffect Effect)> Effects => _effects;

        /// <summary>The creatures and players it hit, in the order it hit them.</summary>
        public IReadOnlyList<Actor> Hit => _hit;

        private QuietLandings(Manifestation player, ActionId actionId, uint actionArgId)
        {
            _player = player;
            ActionId = actionId;
            ActionArgId = actionArgId;
            LandedTick = Environment.TickCount64;
            _outer = _open;
            _open = this;
        }

        /// <summary>Opens the player's recovery of the action, to be closed when it has been resolved: "using (QuietLandings.Open(...))".</summary>
        public static QuietLandings Open(Manifestation player, ActionId actionId, uint actionArgId) =>
            player == null ? null : new QuietLandings(player, actionId, actionArgId);

        /// <summary>An effect GameEffectManager.Attach has just told the clients of, on actor.</summary>
        internal static void Note(Actor actor, GameEffect effect)
        {
            var open = _open;

            if (open == null || actor == null || effect == null)
                return;

            if (effect.AnnounceOnAttach || !effect.AnnounceToCaster || effect.ServerOnly)
                return;

            if ((effect.IsSkillPassive || effect.OwnerOnly) && !ReferenceEquals(actor, open._player))
                return;

            open._effects.Add((actor, effect));
        }

        /// <summary>Damage from source about to be dealt to target.</summary>
        internal static void NoteHit(Actor target, Actor source)
        {
            var open = _open;

            if (open == null || !ReferenceEquals(source, open._player) || !(target is Creature || target is Manifestation))
                return;

            if (!open._hit.Contains(target))
                open._hit.Add(target);
        }

        /// <summary>
        /// A RequestActionInterrupt from the player's client that no action waiting to land took:
        /// when it is for the one that last landed, and not long after, what that did that their
        /// client has not been shown is. Whether anything was sent.
        /// </summary>
        public static bool Reannounce(Client client, ActionId actionId, uint actionArgId)
        {
            var player = client?.Player;

            if (player == null)
                return false;

            var landed = player.QuietlyLanded;

            if (landed == null || landed.ActionId != actionId || landed.ActionArgId != actionArgId
                || Environment.TickCount64 - landed.LandedTick > WindowMs)
                return false;

            // Once: a second interrupt for it finds nothing.
            player.QuietlyLanded = null;

            var stillOn = landed._effects
                .Where(entry => entry.Holder.ActiveEffects.TryGetValue(entry.Effect.EffectId, out var still)
                    && ReferenceEquals(still, entry.Effect) && !entry.Effect.IsExpired && entry.Effect.TypeId > 0)
                .ToList();

            // Each hit or holder once - those hit first, as they were - with the types it holds.
            var holders = landed._hit.Concat(stillOn.Select(entry => entry.Holder)).Distinct().ToList();
            var shown = false;

            foreach (var holder in holders)
            {
                if (!ReferenceEquals(EntityManager.Instance.GetActor(holder.EntityId), holder))
                    continue;

                var types = stillOn.Where(entry => ReferenceEquals(entry.Holder, holder)).Select(entry => (uint)entry.Effect.TypeId).ToList();

                client.CallMethod(holder.EntityId, new AnnounceMapDamagePacket(0, targetEffectIds: types));
                shown = true;
            }

            return shown;
        }

        public void Dispose()
        {
            if (!ReferenceEquals(_open, this))
                return;

            _open = _outer;
            LandedTick = Environment.TickCount64;
            _player.QuietlyLanded = _effects.Count > 0 || _hit.Count > 0 ? this : null;
        }
    }
}
