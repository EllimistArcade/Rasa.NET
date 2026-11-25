using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Structures;

    /// <summary>
    /// How much a creature hates whom, and who it fights because of it. Every creature carries a
    /// HateTable; this is what fills it, empties it and reads it.
    ///
    /// The weights are the client's own, from shared/gameconstants.py - the module the server
    /// shared with the client, whose DamageInfo carries doHate, threatMod and extraThreat on every
    /// hit: DAMAGE_THREAT_MOD 3.0 for each point that landed, ABSORBED_THREAT_MOD 2.0 for each a
    /// shield soaked up, RESISTED_THREAT_MOD 1.0 for each resisted. So a hit is hated for
    /// (landed x 3 + absorbed x 2 + resisted x 1), times the attacker's perceived-threat
    /// modifier - THREAT_MODIFIER_PERCENT on the effects on them (Sacrifice: +30 / +75 / +300 %,
    /// "Perceived Threat: X%").
    ///
    /// The rest the client does not say, and is chosen:
    ///  - noticing someone in the aggro scan puts them on the table with a token 1, so the first
    ///    enemy seen is fought until somebody actually hurts it;
    ///  - healing a player is hated by every creature that already hates that player, one point
    ///    per point healed - the healing tools' "Threat Reduction" modules say healing draws
    ///    threat, and not how much;
    ///  - a new attacker takes the creature over from its current target only when it is hated
    ///    10 % more, so two players doing about the same do not have it turning every think.
    ///
    /// What empties it: an entry that can no longer be fought is dropped as the creature looks
    /// (dead, gone, off the map, cloaked); a cloak (Detection) and Tactical Evasion's mag flash
    /// ("Clear Enemy Hate") take entries off on purpose; and a creature that gives up the fight -
    /// leashed home, or nobody left it can reach - forgets the lot.
    ///
    /// And three more, as the threat design has them:
    ///  - the pulse: every <see cref="PulseIntervalMs"/> a creature that is fighting hates each enemy
    ///    within <see cref="PulseRadius"/> of it <see cref="PulseHate"/> more (at their perceived-threat
    ///    modifier), so whoever stands close keeps its attention - a player in melee holds it. It
    ///    starts no fight: the enemies are those it would seek (players, the other side) and anyone
    ///    already on its table;
    ///  - the call for help: the first time a creature is hurt in a fight it calls the creatures of
    ///    its side within <see cref="CallForHelpRadius"/> that are not fighting into it, each with the
    ///    attacker on its table at the token <see cref="NoticedThreat"/> - what they see and what hurts
    ///    them decides their target from there. Once a fight: it calls again only after its table has
    ///    emptied;
    ///  - the fade: an entry that has gained no hate for <see cref="FadeAfterMs"/> loses
    ///    <see cref="FadePerSecond"/> of its value each second after, and comes off the table below
    ///    <see cref="NoticedThreat"/> - except the creature's target, which keeps that much, so a
    ///    fight is not dropped while the one it is with does nothing to it.
    /// Emplacements and minions neither pulse nor call nor answer a call. A mission scene's actors do
    /// none of the three, and their tables do not fade: their scene directs their fights.
    /// </summary>
    public static class Threat
    {
        public const double DamageThreatMod = 3.0;      // shared/gameconstants.py DAMAGE_THREAT_MOD
        public const double AbsorbedThreatMod = 2.0;    // ABSORBED_THREAT_MOD
        public const double ResistedThreatMod = 1.0;    // RESISTED_THREAT_MOD

        /// <summary>What being noticed in the aggro scan is worth: enough to be on the table.</summary>
        public const double NoticedThreat = 1.0;

        /// <summary>Hate per point of healing done to someone a creature hates.</summary>
        public const double HealThreatMod = 1.0;

        /// <summary>How much more a rival has to be hated to take the creature off its current target.</summary>
        public const int TakeoverPercent = 110;

        /// <summary>What the pulse adds to each enemy near a fighting creature.</summary>
        public const double PulseHate = 100;

        /// <summary>How often a fighting creature pulses.</summary>
        public const long PulseIntervalMs = 2000;

        /// <summary>How near a fighting creature an enemy has to be for its pulse.</summary>
        public const float PulseRadius = 20f;

        /// <summary>How far a creature's call for help reaches.</summary>
        public const float CallForHelpRadius = 60f;

        /// <summary>How long an entry goes without gaining hate before it fades.</summary>
        public const long FadeAfterMs = 5000;

        /// <summary>The share of an entry's hate a second of fading takes off.</summary>
        public const double FadePerSecond = 0.10;

        /// <summary>
        /// The attacker's perceived threat, in percent: 100 plus the THREAT_MODIFIER_PERCENT on
        /// the effects on them, and plus their item modules' "Perceived Threat: -10%"
        /// (ItemModuleBonuses).
        /// </summary>
        public static int ThreatModifierOf(Actor actor)
        {
            if (actor == null)
                return 100;

            var percent = 100 + ItemModuleBonuses.Of(actor).ThreatPercent;

            foreach (var effect in actor.ActiveEffects.Values)
                percent += effect.ThreatModifierPercent;

            return Math.Max(0, percent);
        }

        /// <summary>The hate a hit is worth before the attacker's modifier: landed x 3 + absorbed x 2 + resisted x 1.</summary>
        public static double OfHit(int landed, int absorbed, int resisted)
        {
            return Math.Max(0, landed) * DamageThreatMod + Math.Max(0, absorbed) * AbsorbedThreatMod + Math.Max(0, resisted) * ResistedThreatMod;
        }

        /// <summary>
        /// A creature was hit and survived: the attacker is hated for it, and a creature that was
        /// not fighting anyone turns on them.
        /// </summary>
        public static void FromDamage(Creature victim, Actor attacker, int landed, int absorbed = 0, int resisted = 0)
        {
            if (victim == null || attacker == null || attacker == victim)
                return;

            var hate = OfHit(landed, absorbed, resisted) * ThreatModifierOf(attacker) / 100.0;

            hate = ToSummon(victim, attacker, hate);
            hate = ToMaster(victim, attacker, hate);

            // A hit for nothing still says who is shooting.
            victim.Hate.Ensure(attacker.EntityId, NoticedThreat);
            victim.Hate.Add(attacker.EntityId, hate);

            // A walker on a route that resumes after combat fights from where it was hit: its home is the next waypoint while
            // it walks (SetActionScriptedMove), maybe more than the leash away, and would end the fight at its next think.
            if (victim.Controller?.CurrentAction == BehaviorManager.BehaviorActionScriptedMove && victim.Controller.ScriptedMove?.FightsBack == true)
                victim.HomePos.Position = victim.Position;

            // The first hurt of a fight calls its side round it in.
            if (!victim.CalledForHelp)
            {
                victim.CalledForHelp = true;
                CallForHelp(victim.RuntimeMapChannel ?? MapChannelManager.Instance.FindByContextId(victim.MapContextId), victim, attacker);
            }

            TurnOn(victim, attacker);
        }

        /// <summary>
        /// A control point's defender is told of an enemy (ControlPoints.Alert): it hates them by
        /// <paramref name="hate"/>, and turns on them if it was not fighting, as a hit would have
        /// it do. One already fighting keeps its target unless this puts the newcomer
        /// <see cref="TakeoverPercent"/> above it.
        /// </summary>
        public static void Alerted(Creature defender, Actor enemy, double hate)
        {
            if (defender == null || enemy == null || enemy == defender || hate <= 0)
                return;

            defender.Hate.Add(enemy.EntityId, hate);

            TurnOn(defender, enemy);
        }

        /// <summary>A creature that was not fighting turns on <paramref name="enemy"/>: from wandering, a patrol, a path, its squad or a walk that fights back.</summary>
        private static void TurnOn(Creature creature, Actor enemy)
        {
            // One making for a control point to claim it keeps going (LandingZoneAssaults).
            if (creature.Controller != null && !creature.OnClaimDuty
                && (creature.Controller.CurrentAction == BehaviorManager.BehaviorActionWander
                    || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionPatrol
                    || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionFollowingPath
                    || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionSquad
                    || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionScriptedMove && creature.Controller.ScriptedMove?.FightsBack == true))
                BehaviorManager.Instance.SetActionFighting(creature, enemy.EntityId);
        }

        /// <summary>
        /// A think of the creature's: its table fades, and a creature that is fighting pulses
        /// every <see cref="PulseIntervalMs"/>. One whose table has emptied may call for help again.
        /// </summary>
        public static void Tick(MapChannel mapChannel, Creature creature, long delta)
        {
            if (creature == null || delta <= 0)
                return;

            var fighting = creature.Controller?.CurrentAction == BehaviorManager.BehaviorActionFighting;

            if (creature.Hate.Count > 0 && !IsSceneActor(creature))
                creature.Hate.Fade(delta, FadeAfterMs, FadePerSecond, NoticedThreat, fighting ? creature.Controller.ActionFighting.TargetEntityId : 0);

            if (creature.Hate.Count == 0 && !fighting)
                creature.CalledForHelp = false;

            if (!fighting || !TakesPart(creature))
            {
                creature.HatePulseMs = 0;
                return;
            }

            creature.HatePulseMs += delta;

            if (creature.HatePulseMs < PulseIntervalMs)
                return;

            creature.HatePulseMs -= PulseIntervalMs;
            Pulse(mapChannel, creature);
        }

        /// <summary>
        /// Whether a creature pulses, calls for help and answers a call: not an emplacement, nor a minion,
        /// nor a mission scene's actor.
        /// </summary>
        private static bool TakesPart(Creature creature) =>
            creature.MasterEntityId == 0 && !Emplacements.Is(creature) && !IsSceneActor(creature)
            && Game.Missions.World.CreatureGameplayRules.CanParticipateInCombat(creature);

        /// <summary>
        /// A mission scene's actor (SpawnPool.SceneActorRole): a wave sent at the player, an escort, a
        /// strike force. Its scene says whom it fights and holds that on its table (SceneWorldAdapter), so
        /// its table neither fades nor is pulsed, and it neither calls nor answers a call.
        /// </summary>
        public static bool IsSceneActor(Creature creature) => creature.SpawnPool?.SceneActorRole != null;

        /// <summary>
        /// <see cref="PulseHate"/> on each enemy within <see cref="PulseRadius"/> of the creature that it
        /// can fight: a player or creature of the side it seeks, as its aggro scan would notice them, or
        /// anyone on its table already.
        /// </summary>
        public static void Pulse(MapChannel mapChannel, Creature creature)
        {
            if (mapChannel == null)
                return;

            var near = new Dictionary<ulong, (Vector3 Position, Actor Actor)>();

            foreach (var cell in CellManager.CellsIn(mapChannel, creature.Cells))
            {
                // A player it would notice in its scan - of the side it seeks, no GM set always
                // friendly, close enough for their Stealth Armor - or one it already hates.
                foreach (var client in cell.ClientList)
                    if (client?.Player is Manifestation player && (creature.Hate.Contains(player.EntityId)
                        || TargetCategories.Seeks(creature.TargetCategory, player.CombatCategory) && !player.GmFlagAlwaysFriendly
                        && Vector3.Distance(player.Position, creature.Position) <= PulseRadius * player.DetectionRangePercent / 100f))
                        near[player.EntityId] = (player.Position, player);

                foreach (var other in cell.CreatureList)
                    if (other != creature && (TargetCategories.Seeks(creature.TargetCategory, other.TargetCategory) || creature.Hate.Contains(other.EntityId)))
                        near[other.EntityId] = (other.Position, other);

                // A Hortimonculus, a Personal Waypoint, a force field it is already fighting: near
                // it, it is pulsed on as a player beside it is, or that player takes it away.
                foreach (var obj in cell.DynamicObjectList)
                    if (creature.Hate.Contains(obj.EntityId))
                        near[obj.EntityId] = (obj.Position, null);
            }

            foreach (var (id, (position, actor)) in near)
                if (Vector3.Distance(position, creature.Position) <= PulseRadius && CanFight(creature, id))
                    creature.Hate.Add(id, PulseHate * ThreatModifierOf(actor) / 100.0);
        }

        /// <summary>
        /// The creature's side within <see cref="CallForHelpRadius"/> that is free to fight - idle, wandering,
        /// on a beat or path, in a squad - takes the attacker on: on its table at the token
        /// <see cref="NoticedThreat"/>, and fighting them. Not a talking NPC, nor one with no attack, nor an
        /// escort, a defender, one on a claim or one a scene holds out of fights.
        /// </summary>
        public static void CallForHelp(MapChannel mapChannel, Creature victim, Actor attacker)
        {
            if (mapChannel == null || victim == null || attacker == null || !TakesPart(victim))
                return;

            foreach (var ally in CreatureBuffs.AlliesWithin(mapChannel, victim, victim.Position, CallForHelpRadius))
            {
                var action = ally.Controller?.CurrentAction;

                if (action != BehaviorManager.BehaviorActionIdle && action != BehaviorManager.BehaviorActionWander
                    && action != BehaviorManager.BehaviorActionPatrol && action != BehaviorManager.BehaviorActionFollowingPath
                    && action != BehaviorManager.BehaviorActionSquad)
                    continue;

                if (!TakesPart(ally) || ally.Npc != null || ally.Actions.Count == 0 || ally.OnClaimDuty || ally.ScriptedCombatGate != null
                    || BehaviorManager.IsMissionEscort(ally) || Game.Missions.World.CreatureGameplayRules.IsDefender(ally)
                    || !CanFight(ally, attacker.EntityId) || !BehaviorManager.CanSetActionFighting(ally, attacker.EntityId))
                    continue;

                ally.Hate.Ensure(attacker.EntityId, NoticedThreat);
                BehaviorManager.Instance.SetActionFighting(ally, attacker.EntityId);
            }
        }

        /// <summary>The aggro scan picked somebody out: they are on the table.</summary>
        public static void Noticed(Creature creature, ulong entityId)
        {
            creature?.Hate.Ensure(entityId, NoticedThreat);
        }

        /// <summary>
        /// A healer healed a player: every creature near the healed one that already hates them
        /// hates the healer for it.
        /// </summary>
        public static void FromHealing(MapChannel mapChannel, Actor healer, Actor healed, int amount)
        {
            if (mapChannel == null || healer == null || healed == null || amount <= 0)
                return;

            var hate = amount * HealThreatMod * ThreatModifierOf(healer) / 100.0;

            foreach (var cell in CellManager.CellsIn(mapChannel, healed.Cells))
                foreach (var creature in cell.CreatureList.ToList())
                    if (creature.State != CharacterState.Dead && creature.Hate.Contains(healed.EntityId))
                        creature.Hate.Add(healer.EntityId, ToSummon(creature, healer, hate));
        }

        /// <summary>
        /// A heal done for a healer by something that is no actor - a Hortimonculus: the hate of
        /// every creature near the healed one that hates them goes to that entity, at the
        /// healer's threat modifier.
        /// </summary>
        public static void FromHealing(MapChannel mapChannel, ulong drawnTo, Actor healer, Actor healed, int amount)
        {
            if (mapChannel == null || drawnTo == 0 || healed == null || amount <= 0)
                return;

            var hate = amount * HealThreatMod * ThreatModifierOf(healer) / 100.0;

            foreach (var cell in CellManager.CellsIn(mapChannel, healed.Cells))
                foreach (var creature in cell.CreatureList.ToList())
                    if (creature.State != CharacterState.Dead && creature.Hate.Contains(healed.EntityId))
                        creature.Hate.Add(drawnTo, hate);
        }

        /// <summary>
        /// MINION_HATE_TO_MASTER_PERCENT (Spotter): of the hate a summon earns, its share goes to
        /// its master instead - when the master is a player on the same map. Returns what is left
        /// for the summon.
        /// </summary>
        public static double ToMaster(Creature victim, Actor attacker, double hate)
        {
            if (hate <= 0 || !(attacker is Creature summon) || summon.HateToMasterPercent <= 0 || summon.MasterEntityId == 0)
                return hate;

            if (!EntityManager.Instance.Players.TryGetValue(summon.MasterEntityId, out var master) || master.MapContextId != victim.MapContextId)
                return hate;

            var share = AbilityManager.HateTransferred(hate, summon.HateToMasterPercent);

            victim.Hate.Ensure(master.EntityId, NoticedThreat);
            victim.Hate.Add(master.EntityId, share);

            return hate - share;
        }

        /// <summary>
        /// HATE_TRANSFER_PERCENT: a player with a summon - trap, turret, reality ripper - near the
        /// creature has that share of the hate they have just earned go to the summon instead
        /// (AbilityManager.HateSinkFor). Returns what is left for the player. Only new hate is
        /// split; what the creature already held against the player stays where it is.
        /// </summary>
        public static double ToSummon(Creature victim, Actor attacker, double hate)
        {
            if (hate <= 0 || !(attacker is Manifestation owner))
                return hate;

            var (summon, percent) = AbilityManager.HateSinkFor(owner, victim);

            if (summon == null)
                return hate;

            var share = AbilityManager.HateTransferred(hate, percent);

            victim.Hate.Ensure(summon.EntityId, NoticedThreat);
            victim.Hate.Add(summon.EntityId, share);

            return hate - share;
        }

        /// <summary>
        /// An actor has gone out of reach of every creature around - cloaked: it is taken off their
        /// tables, and those that were fighting it turn to whoever they hate next, or give up.
        /// </summary>
        public static void Forget(MapChannel mapChannel, Actor actor)
        {
            foreach (var cell in CellManager.CellsIn(mapChannel, actor.Cells))
                foreach (var creature in cell.CreatureList.ToList())
                {
                    if (!creature.Hate.Contains(actor.EntityId))
                        continue;

                    creature.Hate.Remove(actor.EntityId);

                    if (creature.Controller?.CurrentAction == BehaviorManager.BehaviorActionFighting
                        && creature.Controller.ActionFighting.TargetEntityId == actor.EntityId
                        && !Retarget(creature))
                        BehaviorManager.Instance.StopFighting(creature);
                }
        }

        /// <summary>"Clear Enemy Hate": the creature forgets everyone and stops fighting.</summary>
        public static void Clear(Creature creature)
        {
            if (creature == null)
                return;

            creature.Hate.Clear();

            if (creature.Controller?.CurrentAction == BehaviorManager.BehaviorActionFighting)
                BehaviorManager.Instance.StopFighting(creature);
        }

        /// <summary>
        /// Whether a creature can fight this entity at all right now: a living player it can see
        /// on its own map, or a living creature on it - either one its target category allows
        /// (BehaviorManager.MayFight), so a table never hands it a target SetActionFighting refuses.
        /// </summary>
        public static bool CanFight(Creature creature, ulong entityId)
        {
            if (!BehaviorManager.MayFight(creature, entityId))
                return false;

            switch (EntityManager.Instance.GetEntityType(entityId))
            {
                case EntityType.Character:
                {
                    var player = EntityManager.Instance.GetPlayer(entityId);

                    // Gone: a connection that has gone, whose character is waiting for the map
                    // worker to take it out. It cannot fight back or be healed, and a creature
                    // that kept at it would be beating on an empty body - unless it dropped out of
                    // this very fight, when the body stays to be fought (CombatLogout).
                    return player != null && !player.IsGone && player.MapContextId == creature.MapContextId
                        && player.State != CharacterState.Dead && player.Attributes[Attributes.Health].Current > 0
                        && !Detection.UnnoticedByCreatures(player) && !CameraScripts.IsWatching(player);
                }

                case EntityType.Creature:
                {
                    var other = EntityManager.Instance.GetCreature(entityId);

                    return other != null && other != creature && other.MapContextId == creature.MapContextId
                        && other.State != CharacterState.Dead && other.State != CharacterState.Dying
                        && other.Attributes[Attributes.Health].Current > 0;
                }

                // A Personal Waypoint, Hortimonculus or force field MayFight let through: it is
                // there and standing, or it is not.
                case EntityType.Object:
                    return PersonalWaypoints.MayBeFoughtBy(creature, entityId) || AbilityManager.PlantMayBeFoughtBy(creature, entityId) || ForceFields.MayBeFoughtBy(creature, entityId);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Who the creature should be fighting now: the most hated it can still fight, the current
        /// target kept unless a rival is hated TakeoverPercent more. Entries that can never be
        /// fought again - dead, gone, off the map - are dropped as it looks; a cloaked one is
        /// only passed over. 0 when nobody on the table can be fought.
        /// </summary>
        public static ulong ChooseTarget(Creature creature)
        {
            // Not Ranked(): nothing here needs an order, and this runs every think.
            creature.Hate.RemoveWhere(id => EntityManager.Instance.GetEntityType(id) == 0);

            var current = creature.Controller?.ActionFighting.TargetEntityId ?? 0;

            return creature.Hate.Top(id => CanFight(creature, id), current, TakeoverPercent);
        }

        /// <summary>
        /// The creature's current target is lost: it is taken off the table and the creature turns
        /// to whoever it hates next. False when there is nobody, and the creature should give up.
        /// </summary>
        public static bool Retarget(Creature creature)
        {
            var lost = creature.Controller?.ActionFighting.TargetEntityId ?? 0;

            if (lost != 0)
                creature.Hate.Remove(lost);

            var next = ChooseTarget(creature);

            if (next == 0)
                return false;

            BehaviorManager.Instance.SetActionFighting(creature, next);
            return true;
        }
    }
}
