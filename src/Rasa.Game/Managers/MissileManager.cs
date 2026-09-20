using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Timer;
    using Structures;
    using Rasa.Game;
    using Rasa.Packets.MapChannel.Client;
    using Rasa.Packets.MapChannel.Server.PerformRecovery;

    public class MissileManager
    {
        private static MissileManager _instance;
        private static readonly object InstanceLock = new object();
        public readonly Timer Timer = new Timer();

        public static MissileManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (InstanceLock)
                {
                    if (_instance == null)
                        _instance = new MissileManager();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Furthest a missile may be aimed. Cells are 25.6 units and a client is only ever told
        /// about the 5x5 cells around it, so nothing past ~64 units is even on its screen; this
        /// is twice that, which no weapon reaches and no honest client asks for.
        /// </summary>
        private const float MaxTargetDistance = 128f;

        private MissileManager()
        {
        }

        /// <summary>
        /// Entity ids are global, but cells are per map: CellCallMethod indexes this map's cell
        /// table with the target's cell seeds, and a target on another map throws
        /// KeyNotFoundException on the main loop. A client keeps its Target across a waypoint
        /// or summon, so this is reachable by pressing fire before re-targeting.
        /// </summary>
        private static bool IsOnMap(MapChannel mapChannel, Actor actor)
        {
            return actor != null && actor.MapContextId == mapChannel.MapInfo.MapContextId;
        }

        /// <summary>
        /// Marks an actor as being in a fight, if it is a player. Creatures have their own notion
        /// of it in BehaviorManager and are not touched here.
        ///
        /// Looked up through the actor's own map rather than Server.Clients: damage is a hot path
        /// and one map's client list is a great deal shorter than every client on the server.
        /// </summary>
        private static void EnterCombat(Actor actor)
        {
            if (!(actor is Manifestation player) || player.MapChannel == null)
                return;

            foreach (var client in player.MapChannel.ClientList)
                if (client.Player == player)
                {
                    ManifestationManager.Instance.EnterCombat(client);
                    return;
                }
        }

        /// <summary>
        /// Applies the victim's resistance (from the effects on them; see
        /// GameEffectManager.ApplyResist) to a missile about to land, so the damage taken and
        /// the damage reported agree, and the hit data says what was resisted.
        /// </summary>
        private static void Resist(Actor victim, Missile missile)
        {
            missile.DamageA = GameEffectManager.ApplyResist(victim, missile.DamageA, out var resisted);

            if (resisted > 0)
                foreach (var hit in missile.Args.HitData)
                    if (hit.EntityId == victim.EntityId)
                    {
                        hit.Resisted = (uint)resisted;
                        hit.FinalAmt = missile.DamageA;
                    }
        }

        /// <summary>
        /// The part of a missile's damage that meets armour. The rest - ArmorBypassPercent of it
        /// - goes past, and with whatever the armour could not stop comes off health: "Bypass
        /// Armor: 25% of damage done directly to Health".
        /// </summary>
        public static int ArmorShare(Missile missile)
        {
            if (missile.ArmorBypassPercent <= 0)
                return missile.DamageA;

            return missile.DamageA - missile.DamageA * missile.ArmorBypassPercent / 100;
        }

        private void DoDamageToCreature(MapChannel mapChannel, Missile missile)
        {
            var creature = EntityManager.Instance.GetCreature(missile.TargetEntityId);

            if (creature.State == CharacterState.Dead)
                return;

            // Shooting something is being in a fight, not only being shot at - otherwise a player
            // who opens fire and wins never enters combat at all.
            EnterCombat(missile.Source);

            Resist(creature, missile);

            // decrease armor first - all of it but what bypasses armour
            var armorDecrease = Math.Min(ArmorShare(missile), creature.Attributes[Attributes.Armor].Current);
            creature.Attributes[Attributes.Armor].Current -= armorDecrease;
            CellManager.Instance.CellCallMethod(mapChannel, creature, new UpdateArmorPacket(creature.Attributes[Attributes.Armor], creature.EntityId));

            // decrease health (if armor is depleted)
            var healthDecrease = Math.Min(missile.DamageA - armorDecrease, creature.Attributes[Attributes.Health].Current);
            creature.Attributes[Attributes.Health].Current -= healthDecrease;
            CellManager.Instance.CellCallMethod(mapChannel, creature, new UpdateHealthPacket(creature.Attributes[Attributes.Health], creature.EntityId));
            
            if (creature.Attributes[Attributes.Health].Current <= 0)
            {
                // fix health so it dont regenerate after death
                missile.TargetActor.Attributes[Attributes.Health].Current = 0;
                missile.TargetActor.Attributes[Attributes.Health].RefreshAmount = 0;
                missile.TargetActor.Attributes[Attributes.Health].RefreshPeriod = 0;

                // fix armor so it dont regenerate after death
                missile.TargetActor.Attributes[Attributes.Armor].Current = 0;
                missile.TargetActor.Attributes[Attributes.Armor].RefreshAmount = 0;
                missile.TargetActor.Attributes[Attributes.Armor].RefreshPeriod = 0;
                // kill craeture
                CreatureManager.Instance.HandleCreatureKill(mapChannel, creature, missile.Source);
            }
            else
            {
                // shooting at wandering creatures makes them ANGRY
                if (creature.Controller.CurrentAction == BehaviorManager.BehaviorActionWander || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionFollowingPath)
                    BehaviorManager.Instance.SetActionFighting(creature, missile.Source.EntityId);

                WeaponBonus(mapChannel, creature, missile);
            }
        }

        private readonly Random _random = new Random();

        /// <summary>
        /// Shredder Ammo: a weapon hit by someone carrying it does the effect's extra damage too,
        /// at most once per its interval. Taken through ActorManager.Damage, so it can kill and
        /// the kill is the shooter's, and shown through the effect's own AnnounceDamage, which
        /// floats it on the target from the shooter.
        /// </summary>
        private void WeaponBonus(MapChannel mapChannel, Creature target, Missile missile)
        {
            var shooter = missile.Source;

            if (shooter == null || target.State == CharacterState.Dead)
                return;

            var now = Environment.TickCount64;

            foreach (var effect in shooter.ActiveEffects.Values)
            {
                if (effect.WeaponBonusMax <= 0 || now < effect.WeaponBonusReadyAt)
                    continue;

                effect.WeaponBonusReadyAt = now + Math.Max(100, effect.WeaponBonusIntervalMs);

                var rolled = AbilityManager.Scale(effect.SourceLevel, _random.Next(effect.WeaponBonusMin, effect.WeaponBonusMax + 1), effect.TickScaleType);
                var amount = GameEffectManager.ApplyResist(target, rolled, out var resisted, effect.WeaponBonusType);
                var taken = ActorManager.Instance.Damage(mapChannel, target, amount, shooter);

                var announce = new GameEffectAnnounceDamagePacket(effect.EffectId);
                announce.Hits.Add(new TickEntry
                {
                    EntityId = target.EntityId,
                    Amount = amount,
                    Resisted = resisted,
                    DamageType = effect.WeaponBonusType,
                    DeathBlow = taken > 0 && target.Attributes[Attributes.Health].Current <= 0
                });

                CellManager.Instance.CellCallMethod(mapChannel, shooter, announce);
                return;
            }
        }

        private void DoDamageToPlayer(MapChannel mapChannel, Missile missile)
        {
            var actor = EntityManager.Instance.GetActor(missile.TargetEntityId);

            if (actor.State == CharacterState.Dead)
                return;

            // Both ends: whoever was hit, and whoever hit them if that was a player too.
            EnterCombat(actor);
            EnterCombat(missile.Source);

            // What the effects on the victim resist comes off first (Rage, Resistance, Sacrifice,
            // Base Wave), and off the missile too, since the recovery packet reports its DamageA
            // as the amount that landed.
            Resist(actor, missile);

            // Then a shield takes its share (Shield Extender, Shield Wave).
            missile.DamageA = GameEffectManager.Instance.ApplyAbsorb(mapChannel, actor, missile.DamageA, out var absorbed);

            if (absorbed > 0)
                foreach (var hit in missile.Args.HitData)
                    if (hit.EntityId == actor.EntityId)
                    {
                        hit.Absorbed = (uint)absorbed;
                        hit.FinalAmt = missile.DamageA;
                    }

            // decrease armor first - all of it but what bypasses armour
            var armorDecrease = Math.Min(ArmorShare(missile), actor.Attributes[Attributes.Armor].Current);

            actor.Attributes[Attributes.Armor].Current -= armorDecrease;
            CellManager.Instance.CellCallMethod(mapChannel, actor, new UpdateArmorPacket(actor.Attributes[Attributes.Armor], 0));

            // decrease health (if armor is depleted)
            var healthDecrease = Math.Min(missile.DamageA - armorDecrease, actor.Attributes[Attributes.Health].Current);

            actor.Attributes[Attributes.Health].Current -= healthDecrease;
            CellManager.Instance.CellCallMethod(mapChannel, actor, new UpdateHealthPacket(actor.Attributes[Attributes.Health], 0));

            if(actor.Attributes[Attributes.Health].Current == 0)
            {
                // we won't die yet :D
                actor.Attributes[Attributes.Health].Current = actor.Attributes[Attributes.Health].CurrentMax;
                //actor.State = CharacterState.Dying;
            }

            if (actor.State == CharacterState.Dying)
            {

            }

            Reflect(mapChannel, actor, missile);
        }

        /// <summary>
        /// Reflective Armor: ReflectPercent of what the attack did comes back at a creature that
        /// made it. The wearer has already taken all of it ("User still takes full damage"). The
        /// creature takes it through ActorManager.Damage, so a reflection can kill and the kill
        /// is the wearer's; the wearer's client is told through the hidden MEDIUM_ARMOR_SKILL
        /// effect's AnnounceReflect, which flies the hit back and floats it on the creature.
        /// </summary>
        private static void Reflect(MapChannel mapChannel, Actor victim, Missile missile)
        {
            if (!(missile.Source is Creature attacker) || attacker.State == CharacterState.Dead)
                return;

            var percent = GameEffectManager.ReflectPercentOf(victim);

            if (percent <= 0 || missile.DamageA <= 0)
                return;

            var amount = missile.DamageA * percent / 100;

            if (amount <= 0)
                return;

            var carrier = victim.ActiveEffects.Values.FirstOrDefault(e => e.ReflectPercent > 0);
            var taken = ActorManager.Instance.Damage(mapChannel, attacker, amount, victim);

            if (carrier == null || !(victim is Manifestation player))
                return;

            var client = mapChannel.ClientList.Find(c => c?.Player == player);

            client?.CallMethod(player.EntityId, new GameEffectAnnounceReflectPacket(carrier.EffectId, attacker.EntityId, DamageType.Physical, amount,
                taken > 0 && attacker.Attributes[Attributes.Health].Current <= 0));
        }

        public void RequestWeaponAttack(Client client, RequestWeaponAttackPacket packet)
        {
            // The alternate attack is a melee swing, not a shot: no ammunition, no heat, the
            // weapon's alt damage, and Hand to Hand's bonus rather than the weapon skill's. It
            // used to come through here as one more shot of the gun.
            if (packet.IsAltAction)
            {
                ManifestationManager.Instance.TryMeleeAttack(client, packet);
                return;
            }

            ManifestationManager.Instance.PlayerTryFireWeapon(client);

                /*

                var weapon = InventoryManager.Instance.CurrentWeapon(client);

                if (weapon == null)
                {
                    Logger.WriteLog(LogType.Error, "no weapon armed but player tries to shoot");
                    return;
                }

                var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(weapon);
                var targetType = EntityManager.Instance.GetEntityType((uint)packet.TargetId);
                var target = new Actor();

                switch (targetType)
                {
                    case EntityType.Creature:
                        target = EntityManager.Instance.GetCreature((uint)packet.TargetId).Actor;
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"RequestWeaponAttack:\nUnsuported targetType = {targetType}");
                        return; ;
                }

                var distance = Vector3.Distance(client.MapClient.Player.Actor.Position, target.Position);
                var triggerTime = (int)Math.Round(distance, 0);

                var missile = new Missile
                {
                    ActionId = packet.ActionId,
                    ActionArgId = packet.ActionArgId,
                    TargetEntityId = (uint)packet.TargetId,
                    DamageA = weaponClassInfo.DamageType,
                    IsAbility = false,
                    Source = client,
                    TriggerTime = triggerTime
                };


                missile.TriggerTime = triggerTime;


                QueuedMissiles.Add(missile);
                */
        }

        /// <summary>
        /// Lands the missiles whose flight time has run out, and only those.
        ///
        /// Every queued missile used to be triggered on the pass after it was launched, whatever
        /// its trigger time said - the distance to the target was measured, written on the
        /// missile and never read. It rarely showed: the world loop runs every 100 ms and the
        /// longest shot in range flies for 64, so the tick a missile was due on was almost always
        /// the tick it got. It is the second half that mattered: the queue was emptied after the
        /// loop rather than as it was walked, so a missile that threw on the way in took the
        /// emptying with it, left every queued missile where it was, and threw again on the next
        /// pass - out of the map worker, which abandons the rest of that tick and every map after
        /// it, for as long as the server ran. A missile comes off the queue before it is
        /// triggered now, and a trigger that fails costs only itself.
        /// </summary>
        public void DoWork(MapChannel mapChannel, long delta)
        {
            for (var i = mapChannel.QueuedMissiles.Count - 1; i >= 0; i--)
            {
                var missile = mapChannel.QueuedMissiles[i];

                missile.TriggerTime -= delta;

                if (missile.TriggerTime > 0)
                    continue;

                mapChannel.QueuedMissiles.RemoveAt(i);

                try
                {
                    MissileTrigger(mapChannel, missile);
                }
                catch (Exception e)
                {
                    Logger.WriteLog(LogType.Error,
                        $"Missile {missile.ActionId} from {missile.Source?.EntityId} at {missile.TargetEntityId} on map {mapChannel.MapInfo.MapContextId} threw and was dropped: {e}");
                }
            }
        }

        /// <param name="armorBypassPercent">Percent of the damage that skips armour: the Torqueshell and Injection Gun skills.</param>
        public void MissileLaunch(MapChannel mapChannel, ActionData action, int damage, int armorBypassPercent = 0, DamageType damageType = 0)
        {
            var missile = new Missile
            {
                DamageA = damage,
                DamageType = damageType,
                ArmorBypassPercent = Math.Max(0, Math.Min(100, armorBypassPercent)),
                Source = action.Actor
            };

            // get distance between actors
            Actor targetActor = null;
            var triggerTime = 0; // time between windup and recovery

            if (action.TargetId != 0)
            {
                // target on entity
                var targetType = EntityManager.Instance.GetEntityType(action.TargetId);

                if (targetType == 0)
                {
                    Logger.WriteLog(LogType.Error, $"The missile target doesnt exist: {action.TargetId}");
                    // entity does not exist
                    return;
                }
                switch (targetType)
                {
                    case EntityType.Creature:
                        {
                            targetActor = EntityManager.Instance.GetCreature(action.TargetId);
                            missile.TargetEntityId = action.TargetId;
                        }
                        break;
                    case EntityType.Character:
                        {
                            targetActor = EntityManager.Instance.GetPlayer(action.TargetId);
                            missile.TargetEntityId = action.TargetId;
                        }
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"Can't shoot that object");
                        return;
                };

                if (targetActor == null || targetActor.State == CharacterState.Dead)
                    return; // actor is dead, cannot be shot at

                if (!IsOnMap(mapChannel, targetActor))
                {
                    Logger.WriteLog(LogType.Debug, $"MissileLaunch: {action.Actor.EntityId} aimed at {action.TargetId}, which is on map {targetActor.MapContextId}, not {mapChannel.MapInfo.MapContextId}");
                    return;
                }

                var distance = Vector3.Distance(targetActor.Position, action.Actor.Position);

                if (distance > MaxTargetDistance)
                {
                    Logger.WriteLog(LogType.Debug, $"MissileLaunch: {action.Actor.EntityId} aimed at {action.TargetId} from {distance:F0} units away");
                    return;
                }

                triggerTime = (int)(distance * 0.5f);
            }
            else
            {
                // has no target -> Shoot towards looking angle
                targetActor = null;
                triggerTime = 0;
            }

            // Weapons only: abilities resolve in AbilityManager from their own tables, and the
            // lightning special case that used to fire from here on a hand-typed damage roll
            // went with them.
            missile.TargetActor = targetActor;
            missile.TriggerTime = triggerTime;
            missile.ActionId = action.ActionId;
            missile.ActionArgId = action.ActionArgId;
            missile.IsAbility = false;

            CellManager.Instance.CellCallMethod(mapChannel, action.Actor, new PerformWindupPacket(PerformType.ThreeArgs, missile.ActionId, missile.ActionArgId, missile.TargetEntityId));

            mapChannel.QueuedMissiles.Add(missile);
        }

        public void MissileTrigger(MapChannel mapChannel, Missile missile)
        {
            // ToDo: Some weapons can hit multiple targets
            var targetType = EntityManager.Instance.GetEntityType(missile.TargetEntityId);
            var hitData = new HitData
            {
                FinalAmt = missile.DamageA,
                EntityId = missile.TargetEntityId
            };

            missile.Args.HitEntities.Add(missile.TargetEntityId);
            missile.Args.HitData.Add(hitData);     // ToDo: add suport for multiple targets

            // Checked again here: the missile was queued a tick ago, and the target can have
            // left the map (or the world) since.
            if (missile.TargetEntityId != 0 && !IsOnMap(mapChannel, missile.TargetActor))
                targetType = 0;

            switch (targetType)
            {
                case 0:
                    // no target => ToDo
                    break;
                case EntityType.Creature:
                    DoDamageToCreature(mapChannel, missile);
                    break;
                case EntityType.Character:
                    DoDamageToPlayer(mapChannel, missile);
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"WeaponAttackRecovery: Unsuported targetType {targetType}.");
                    break;
            }

            switch (missile.ActionId)
            {
                case ActionId.WeaponAttack:
                // Melee (174) is resolved exactly like a ranged attack: the original server's
                // missile_ActionRecoveryHandler_WeaponMelee forwarded to the WeaponAttack
                // handler "until there is better handling for melee weapons", and the recovery
                // packet is the same shape. It fell through to the default here, which did the
                // right thing but logged every swing as an unsupported action.
                case ActionId.WeaponMelee:
                    CellManager.Instance.CellCallMethod(mapChannel, missile.Source, new WeaponAttackRecovery(missile));
                    break;
                //else if (missile->actionId == 203)
                //    missile_ActionHandler_CR_FOREAN_LIGHTNING(mapChannel, missile);
                //else if (missile->actionId == 211)
                //    missile_ActionHandler_CR_AMOEBOID_SLIME(mapChannel, missile);
                //else if (missile->actionId == 397)
                //    missile_ActionRecoveryHandler_ThraxKick(mapChannel, missile);
                default:
                    Logger.WriteLog(LogType.Debug, $"MissileLaunch: unsupported missile actionId {missile.ActionId} - using default: WeaponAttackRecovery");
                    CellManager.Instance.CellCallMethod(mapChannel, missile.Source, new WeaponAttackRecovery(missile));
                    break;
            }
        }
    }
}
