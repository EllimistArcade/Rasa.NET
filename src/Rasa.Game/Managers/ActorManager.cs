using System;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Structures;

    public class ActorManager
    {
        /*      Actor Packets:
         *  - PreloadData(self, weaponId, abilities)
         *  - AppearanceData(self, appearanceData)
         *  - Level(self, level)
         *  - AttributeInfo(self, attrDict)
         *  - ResistanceData(self, resistDataDict)
         *  - TargetCategory(self, targetCategory)
         *  - ToBePerceivedModifier(self, mod)
         *  - ToPerceiveModifier(self, mod)
         *  - UpdateHealth(self, current, currentMax, refreshAmount, whoId)
         *  - UpdatePower(self, current, currentMax, refreshAmount, whoId)
         *  - UpdateArmor(self, current, currentMax, refreshAmount, whoId)
         *  - UpdateChi(self, current, currentMax, refreshAmount, whoId)
         *  - UpdateAttributes(self, attributeDataList, whoId)
         *  - UpdateRegions(self, regionIdList)
         *  - ActionBlockChange(self, actionId, isBlocked)
         *  - PerformWindup(self, actionId, actionArgId, *args)
         *  - PerformRecovery(self, actionId, actionArgId, *args)
         *  - StateChange(self, stateIdList)
         *  - StateCorrection(self, stateList)
         *  - Abilities(self, abilityList)
         *  - Skills(self, skillList)
         *  - UserActionFailed(self, actionId, actionArgId, msgId)
         *  - ActionFailed(self, actionId, actionArgId)
         *  - PreTeleport(self, teleportType = None)
         *  - TeleportFailed(self)
         *  - PostTeleport(self)
         *  - TeleportArrival(self, delayMs = 0, delayEffectMs = 0, doFade = 0)
         *  - Teleport(self, position, yaw, teleportType = None, delay = 0, doCancel = 1)
         *  - SetTrackingTarget(self, targetId)
         *  - WeaponReady(self, isWeaponReady)
         *  - EquipmentInfo(self, equipmentInfo)
         *  - IsRunning(self, isRunning)
         *  - ActorInfo(self, stateIds, yaw, trackingTarget, movementMod, desiredPostureId, isHoldingCombatMode)
         *  - ActorControllerInfo(self, isPlayer)
         *  - ActorName(self, name)
         *  - SetDesiredCrouchState(self, desiredStateId)
         *  - RequestVisualCombatMode(self, goToCombatMode)
         *  - LevelUp(self, newLevel)
         *  - PlayerDead(self, sourceId, graveyardList, canRevive = 0)
         *  - AnnounceMapDamage(self, rawInfo)
         *  - MadeDead(self)
         *  - ActorKilled(self)
         *  - Revived(self, sourceId)
         *  - DeadOnArrival(self, canRevive)
         *  - ActionReuseTimes(self, reuseTimeList)
         *  - ActionReuseTimerRestarted(self, actionId, actionArgId)
         *  - TargetId(self, targetId)
         *  - ActionInterrupt(self, sourceId, actionId, actionArgId)
         *  - MovementModChange(self, newMovementMod)
         *  - WargameData(self, wargameData)
         *  
         *      Actor Hanlders:
         *  - BuryMe                    => ToDo
         *  - ReviveMe                  => ToDo
         *  - RequestActionInterrupt    => ToDo
         *  - RequestDetachGameEffect   => gesture effects only, GestureManager
         *  - RequestVisualCombatMode   => ToDo
         *  - SetDesiredCrouchState     => ToDo
         *  - TeleportAcknowledge       => ToDo
         */

        private static ActorManager _instance;
        private static readonly object InstanceLock = new object();
        public static ActorManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new ActorManager();
                    }
                }

                return _instance;
            }
        }

        private ActorManager()
        {
        }
        #region Handlers

        public void RequestActionInterrupt(Client client, RequestActionInterruptPacket packet)
        {
            foreach (var action in client.Player.MapChannel.PerformRecovery)
                if (action.Actor == client.Player)
                    if (action.ActionId == packet.ActionId)
                        if (action.ActionArgId == packet.ActionArgId)
                        {
                            action.IsInrerrupted = true;
                            break;
                        }
        }

        public void RequestVisualCombatMode(Client client, bool combatMode)
        {
            client.Player.InCombatMode = combatMode;
            client.CellCallMethod(client, client.Player.EntityId, new RequestVisualCombatModePacket(combatMode));
        }

        public void SetDesiredCrouchState(Client client, CharacterState state)
        {
            client.CellIgnoreSelfCallMethod(client, new SetDesiredCrouchStatePacket(state));
        }

        #endregion

        #region Health

        /// <summary>
        /// Raises an actor's health and tells everyone who can see them. Returns how much was
        /// actually put back, which is not what was asked for when the actor was nearly full.
        ///
        /// This is the one place health goes up. Everything that heals - an ability, armour or
        /// health regeneration, a medkit, a console command - goes through here, so the clamp,
        /// the refusal to heal the dead and the update to every onlooker are decided once.
        /// </summary>
        /// <param name="sourceEntityId">
        /// Whose doing it was, and it decides whether the client announces the change itself.
        /// Actor.UpdateAttribute in the client starts with
        /// "announce = not entitymanager.HasEntity(whoId)": if the client knows the entity it
        /// stays quiet, because whatever that entity did is expected to announce the healing
        /// with the source attached - HealAbility.DoAbility walks its own hit list and calls
        /// target.AnnounceHealing(sourceId, amount) client-side. If the client does not know the
        /// entity, and 0 is never a real one, it announces the change itself. So pass the
        /// healer's entity id when a visible actor's ability did it, and leave it 0 for
        /// regeneration, a command, or anything else with no actor behind it.
        /// </param>
        public int Heal(Actor target, int amount, ulong sourceEntityId = 0)
        {
            if (target == null || amount <= 0)
                return 0;

            if (!target.Attributes.TryGetValue(Attributes.Health, out var health))
                return 0;

            // Healing the dead is not healing. Health above zero would not bring them back -
            // nothing about their state would have changed - and it would leave a corpse
            // standing there with a full bar. Coming back is Revived, which is not wired yet.
            if (target.State == CharacterState.Dead || health.Current <= 0)
                return 0;

            var applied = Math.Min(amount, health.CurrentMax - health.Current);

            if (applied <= 0)
                return 0;

            health.Current += applied;

            var mapChannel = MapChannelManager.Instance.FindByContextId(target.MapContextId);

            // No map means nobody can see them, which is not a reason to refuse the heal - the
            // health is still theirs. It is a reason not to try to broadcast it.
            if (mapChannel != null)
                CellManager.Instance.CellCallMethod(mapChannel, target, new UpdateHealthPacket(health, sourceEntityId));

            return applied;
        }

        /// <summary>Puts an actor back to its maximum, and says how much that took.</summary>
        public int HealToFull(Actor target, ulong sourceEntityId = 0)
        {
            if (target == null || !target.Attributes.TryGetValue(Attributes.Health, out var health))
                return 0;

            return Heal(target, health.CurrentMax, sourceEntityId);
        }

        /// <summary>
        /// Raises an actor's armour and tells everyone who can see them, returning how much went
        /// back on. The armour counterpart of <see cref="Heal"/>, and the same reasoning applies
        /// to all of it: one place for the clamp, one for the broadcast, and the dead are
        /// refused - MissileManager zeroes armour and its regeneration on death, so putting a
        /// number back would leave a corpse wearing armour that never ticks.
        ///
        /// <paramref name="sourceEntityId"/> works as it does for healing: an entity the client
        /// knows means the client leaves the announcement to whatever that entity is doing.
        /// </summary>
        public int RestoreArmor(Actor target, int amount, ulong sourceEntityId = 0)
        {
            if (target == null || amount <= 0)
                return 0;

            if (!target.Attributes.TryGetValue(Attributes.Armor, out var armor))
                return 0;

            if (target.State == CharacterState.Dead)
                return 0;

            var applied = Math.Min(amount, armor.CurrentMax - armor.Current);

            if (applied <= 0)
                return 0;

            armor.Current += applied;

            var mapChannel = MapChannelManager.Instance.FindByContextId(target.MapContextId);

            if (mapChannel != null)
                CellManager.Instance.CellCallMethod(mapChannel, target, new UpdateArmorPacket(armor, target.EntityId));

            return applied;
        }

        #endregion
    }
}
