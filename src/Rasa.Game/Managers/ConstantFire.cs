using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// Constant-fire weapons, as the client plays them (client/actions/weapons/constantfire.py):
    /// holding the trigger puts a ConstantFireEffect on the shooter, attached with (weaponId,
    /// actionId, actionArgId, numShots, interval, burstRecoil), and every interval the server
    /// ticks it with that interval's pulses of hits; letting go takes it off, which is when the
    /// client stops charging the weapon. Nothing is fired as a missile, and there is no windup
    /// and recovery per shot - the effect's ticks are the shots.
    ///
    /// Only the leech gun (WEAPON_DENSITYGUN, CF_DENSITY_GUN_EFFECT 94) is fired this way so far.
    /// Its ticks also carry the leech: "Damage Conversion: 25% of damage done / Conversion Radius:
    /// 10m" at every pump of Leech Guns, which the skill describes as converting "a percentage of
    /// the damage done to a target into health or armor that is applied to the user and nearby
    /// squad members". The client's DensityGunEffect announces each as (entityId, healAmount,
    /// repairAmount); health is healed first and what a full health bar cannot take repairs
    /// armour. Machine guns, flamethrowers and polarity guns have constant-fire effects of their
    /// own (CF_MACHINEGUN_EFFECT and the rest) and are still fired as single shots.
    ///
    /// The server's auto-fire timer is what drives it: each refire the timer's shot comes here
    /// instead of MissileManager, the first one attaching the effect; StopAutoFire, a shot that
    /// could not be fired (out of ammo, jammed, weapon gone) or a client that stopped keeping the
    /// fire alive takes it off. One pulse of one shot a refire (800 ms for the leech guns in the
    /// data): SHOTS_PER_INTERVAL and DAMAGE_INTERVAL are weapon properties that survive in
    /// nothing we have.
    /// </summary>
    public static class ConstantFire
    {
        public const int DensityGunTypeId = 94;             // CF_DENSITY_GUN_EFFECT

        /// <summary>"Damage Conversion: 25% of damage done".</summary>
        public const int LeechPercent = 25;

        /// <summary>"Conversion Radius: 10m".</summary>
        public const float LeechRadius = 10f;

        private sealed class Session
        {
            public Client Client;
            public GameEffect Effect;
            public ActionId ActionId;
            public uint ActionArgId;
        }

        private static readonly Dictionary<Client, Session> Sessions = new Dictionary<Client, Session>();
        private static readonly object SessionsLock = new object();

        /// <summary>Whether this weapon fires constantly rather than shot by shot.</summary>
        public static bool Handles(WeaponClassInfo weapon) => weapon != null && weapon.WeaponAttackActionId == ActionId.WeaponDensitygun;

        public static bool IsFiring(Client client)
        {
            lock (SessionsLock)
                return Sessions.ContainsKey(client);
        }

        /// <summary>How a leech's healing splits for one person: health up to what they are missing, the rest to armour.</summary>
        public static (int Heal, int Repair) Split(int amount, int healthMissing, int armorMissing)
        {
            if (amount <= 0)
                return (0, 0);

            var heal = Math.Min(amount, Math.Max(0, healthMissing));
            var repair = Math.Min(amount - heal, Math.Max(0, armorMissing));

            return (heal, repair);
        }

        /// <summary>
        /// One interval of fire at the shooter's target: the effect attached if it was not, the
        /// shot resolved, the leech shared out, and the tick sent.
        /// </summary>
        public static void Pulse(MapChannel mapChannel, Client client, Item weapon, ActionData action, int damage, DamageType damageType, double critBonus)
        {
            var player = client.Player;
            var session = Begin(mapChannel, client, weapon, action);

            // Firing is firing: it is a fight, and it gives a cloaked shooter away.
            ManifestationManager.Instance.EnterCombat(client);
            Stealth.Break(mapChannel, player);

            var tick = new ConstantFireTickPacket(session.Effect.EffectId, true);
            var pulse = new List<TickEntry>();

            tick.Pulses.Add(pulse);

            if (ResolveTarget(mapChannel, player) is Creature target)
            {
                var rolled = damage;
                var crit = CriticalHits.Resolve(player, target, false, CriticalHits.AttackerChance(player, false, critBonus), ref rolled);
                var amount = GameEffectManager.ApplyResist(target, rolled, out var resisted, damageType);
                var landed = ActorManager.Instance.Damage(mapChannel, target, amount, player, damageType);

                pulse.Add(new TickEntry
                {
                    EntityId = target.EntityId,
                    Amount = amount,
                    Resisted = resisted,
                    DamageType = damageType,
                    IsCritical = crit,
                    DeathBlow = landed > 0 && target.Attributes[Attributes.Health].Current <= 0
                });

                if (crit && target.State != CharacterState.Dead && target.State != CharacterState.Dying && target.Attributes[Attributes.Health].Current > 0)
                    CritEffects.OnCritical(mapChannel, target, player, damageType, amount);

                Leech(mapChannel, player, landed, tick);
            }

            CellManager.Instance.CellCallMethod(mapChannel, player, tick);
        }

        /// <summary>
        /// The trigger is let go, the weapon cannot fire, or the shooter is gone: the effect comes
        /// off, and the action it was part of is ended on the clients.
        /// </summary>
        public static void Stop(Client client)
        {
            Session session;

            lock (SessionsLock)
            {
                if (!Sessions.TryGetValue(client, out session))
                    return;

                Sessions.Remove(client);
            }

            var mapChannel = client.Player?.MapChannel;

            if (mapChannel == null || client.Player == null)
                return;

            GameEffectManager.Instance.DettachEffect(mapChannel, client.Player, session.Effect);

            CellManager.Instance.CellCallMethod(mapChannel, client.Player,
                new PerformRecoveryPacket(PerformType.ListOfArgs, session.ActionId, session.ActionArgId, new MissileArgs()));
        }

        private static Session Begin(MapChannel mapChannel, Client client, Item weapon, ActionData action)
        {
            lock (SessionsLock)
                if (Sessions.TryGetValue(client, out var running) && client.Player.ActiveEffects.ContainsKey(running.Effect.EffectId))
                    return running;

            var player = client.Player;
            var info = weapon.ItemTemplate.WeaponInfo;
            var interval = (int)Math.Max(100, info.Refire);

            var effect = new GameEffect
            {
                TypeId = DensityGunTypeId,
                EffectId = GameEffectManager.Instance.NextEffectId(mapChannel),
                EffectLevel = 1,
                SourceId = player.EntityId,
                Source = player,
                SourceLevel = player.Level,
                IsBuff = true,
                AnnounceOnAttach = true
            };

            // The windup the other clients see the shooter hold for as long as the fire lasts.
            CellManager.Instance.CellCallMethod(mapChannel, player,
                new PerformWindupPacket(PerformType.ThreeArgs, action.ActionId, action.ActionArgId, action.TargetId));

            // ConstantFireEffect.OnAttach(target, weaponId, actionId, actionArgId, numShots, interval, burstRecoil).
            GameEffectManager.Instance.Attach(mapChannel, player, effect,
                weapon.EntityId, (int)action.ActionId, (int)action.ActionArgId, 1, interval, (int)info.RecoilAmount);

            var session = new Session { Client = client, Effect = effect, ActionId = action.ActionId, ActionArgId = action.ActionArgId };

            lock (SessionsLock)
                Sessions[client] = session;

            return session;
        }

        /// <summary>What the shooter is aiming at, if it is still there to be hit.</summary>
        private static Actor ResolveTarget(MapChannel mapChannel, Manifestation player)
        {
            if (player.Target == 0)
                return null;

            var target = EntityManager.Instance.GetActor(player.Target);

            if (target == null || target.MapContextId != mapChannel.MapInfo.MapContextId
                || target.State == CharacterState.Dead || target.State == CharacterState.Dying)
                return null;

            return target;
        }

        /// <summary>
        /// A quarter of what the shot took off the target, healed onto the shooter and the squad
        /// within ten metres - health first, then armour - and listed for the client to announce.
        /// </summary>
        private static void Leech(MapChannel mapChannel, Manifestation player, int landed, ConstantFireTickPacket tick)
        {
            var amount = landed * LeechPercent / 100;

            if (amount <= 0)
                return;

            foreach (var ally in AbilityManager.SquadWithin(mapChannel, player, LeechRadius))
            {
                var health = ally.Attributes[Attributes.Health];
                var armor = ally.Attributes[Attributes.Armor];
                var (heal, repair) = Split(amount, health.CurrentMax - health.Current, armor.CurrentMax - armor.Current);

                if (heal > 0)
                    heal = ActorManager.Instance.Heal(ally, heal, player.EntityId);

                if (repair > 0)
                {
                    armor.Current += repair;
                    CellManager.Instance.CellCallMethod(mapChannel, ally, new UpdateArmorPacket(armor, player.EntityId));
                }

                if (heal > 0 || repair > 0)
                    tick.Heals.Add((ally.EntityId, heal, repair));
            }
        }
    }
}
