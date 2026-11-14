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
    /// What a player's ability put on quietly as it landed, for their own client to be shown
    /// should it cancel the action before it has played it.
    ///
    /// An ability's effect goes on with announce off and the recovery names it: the client's
    /// AddEffectAbility.DoAbility, and a damage ability's hit, call AnnounceGameEffectAttach for
    /// it - make it visible, post its status icon (PhysicalEntity.AnnounceGameEffectAttach). That
    /// call is the action's resolution, which waits for the strike or for the end of the
    /// recovery (BaseActorAction.DoAction, _resolutionMethod). A player who starts walking in that
    /// time interrupts it on their own side - a moveInterrupts action, AttemptInterruptAction -
    /// and Destroy throws the resolution away. Their RequestActionInterrupt reaches a server that
    /// has already landed the ability: the item used up, the cost paid, the effect on. Nothing
    /// was going to announce it, and an Experience Booster went that way - used, its boost
    /// working, and nothing on the player's bar to say so.
    ///
    /// So while a player's recovery is resolved (<see cref="Open"/>), GameEffectManager.Attach
    /// tells <see cref="Note"/> of each effect it puts on quietly, and the last recovery's are
    /// kept on the player. An interrupt for that action that comes after it has landed
    /// (<see cref="Reannounce"/>) has those still on announced on the player's own client by a
    /// hit of nothing: an AnnounceMapDamage whose rawInfo does no damage and names their types in
    /// targetEffectIds. Actor.AnnounceDamage announces one of each type that has not been yet -
    /// what the recovery's hit would have done - and with nothing dealt it floats no number,
    /// writes no combat log line and plays no reaction (combatmessages.OnCombatDamage,
    /// AnnounceDamage). Nobody else cancelled anything: the others play the recovery to its end.
    ///
    /// An effect the client had announced already - an action whose strike had fired before the
    /// player moved - is left as it is: AnnounceGameEffectAttach finds none of its type left to
    /// announce and only notes the type for the next one attached in ten seconds
    /// (PhysicalEntity.__delayedAnnounce). Taking it off and putting it back announced would have
    /// done the same for the one that had not been, and made the status icon of the one that had
    /// blink.
    ///
    /// Only effects a newcomer is shown announced (GameEffect.AnnounceToNewcomers): one kept quiet
    /// for good, a vampiric drain's or a run speed module's, stays so.
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

        public ActionId ActionId { get; }
        public uint ActionArgId { get; }
        public long LandedTick { get; internal set; }

        public IReadOnlyList<(Actor Holder, GameEffect Effect)> Effects => _effects;

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

            if (effect.AnnounceOnAttach || !effect.AnnounceToNewcomers || effect.ServerOnly)
                return;

            if ((effect.IsSkillPassive || effect.OwnerOnly) && !ReferenceEquals(actor, open._player))
                return;

            open._effects.Add((actor, effect));
        }

        /// <summary>
        /// A RequestActionInterrupt from the player's client that no action waiting to land took:
        /// when it is for the one that last landed quietly, and not long after, what that put on
        /// and is still on is shown to the client announced. Whether anything was.
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

            // One hit for each holder, naming the types of what it holds that is still on.
            var shown = false;

            foreach (var group in landed._effects
                .Where(entry => entry.Holder.ActiveEffects.TryGetValue(entry.Effect.EffectId, out var still)
                    && ReferenceEquals(still, entry.Effect) && !entry.Effect.IsExpired && entry.Effect.TypeId > 0)
                .GroupBy(entry => entry.Holder))
            {
                var types = group.Select(entry => (uint)entry.Effect.TypeId).ToList();

                client.CallMethod(group.Key.EntityId, new AnnounceMapDamagePacket(0, targetEffectIds: types));
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
            _player.QuietlyLanded = _effects.Count > 0 ? this : null;
        }
    }
}
