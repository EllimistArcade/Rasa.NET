using System;
using System.Collections.Generic;
using System.Numerics;
using Rasa.Models;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets;
    using Packets.Communicator.Server;
    using Packets.Game.Server;
    using Packets.Manifestation.Client;
    using Packets.Manifestation.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;
    public class ManifestationManager
    {
        /* Actor: Player "bodies" (ManifestationClass)
         * 
         *    Manifestation Packets:
         *  - CurrentCharacterId                => implemented
         *  - AllCredits                        => implemented
         *  - UpdateCredits                     => implemented
         *  - LockboxFunds                      => implemented
         *  - WonBattleground                   => ToDo
         *  - LostBattleground                  => ToDo
         *  - WeaponDrawerSlot                  => implemented
         *  - AbilityDrawerSlot                 => implemented
         *  - AbilityDrawer                     => implemented
         *  - ArmWeaponFailed                   => ToDo
         *  - ArmAbilityFailed                  => ToDo
         *  - AdvancementStats                  => implemented
         *  - ExperienceChanged                 => ToDo
         *  - LevelChanged                      => ToDo
         *  - CharacterClass                    => implemented
         *  - AvailableAllocationPoints
         *  - AvailableCharacterClasses
         *  - TierAdvancementInfo
         *  - InvitedToJoinFriend
         *  - InvitationDeclined
         *  - InvitationCancelled
         *  - CannotInvite
         *  - InvitedToAddAndJoinFriend
         *  - RequestToJoin
         *  - JoinFriendDeclined
         *  - JoinFriendCancelled
         *  - CannotJoin
         *  - ForceConverse
         *  - LogosStoneTabula
         *  - LogosStoneAdded
         *  - LogosStoneRemoved
         *  - ShowHelmetChanged
         *  - Titles
         *  - TitleChanged
         *  - TitleAdded
         *  - TitleRemoved
         *  - PlayerFlags
         *  - CloneCredits
         *  - WaypointGained
         *  - GraveyardGained
         *  - CharacterName
         *  - RaceId
         *  - PlayerAfk                        => implemented
         *  - PlayerInactiveWarning            => implemented
         *  - ClanId
         *  - IsTrialAccount                   => implemented (always false)
         *  - PlayerEnteredCombat
         *  - PlayerExitedCombat
         *  - MinionAdded
         *  - MinionStayAck
         *  - MinionGoAck
         *  - MinionFollowMeAck
         *  - MinionFollowTargetAck
         *  - MinionTargetMeAck
         *  - MinionTargetAck
         *  - MinionAssistMeAck
         *  - MinionAssistTargetAck
         *  - MinionTemperamentAck
         *  - MinionCommandAck
         *  
         *  Manifestation Handlrs:
         *  - AutoFireKeepAlive         => ToDo
         *  - ChangeShowHelmet          => ToDo
         *  - ChangeTitle               => implemented, but need more work on it
         *  - RespondToAddAndJoinFriend => ToDo
         *  - RespondToJoinFriend       => ToDo
         *  - RespondToRequestToJoin    => ToDo
         *  - RequestArmAbility         => implemented
         *  - RequestArmWeapon          => implemented
         *  - RequestSetAbilitySlot     => implemented
         *  - RequestSwapAbilitySlots   => implemented
         *  - StartAutoFire             => implemented, but need more work on it
         *  - StopAutoFire              => implemented, but need more work on it
         */
        private static ManifestationManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        private static List<AutoFireTimer> AutoFire = new List<AutoFireTimer>();

        /// <summary>
        /// How far a shot may be handled from when it was due, early or late, without the shot
        /// clock holding it against the player, in ms.
        ///
        /// A shot is timed when it is handled, which is a tick at a time, after a network that
        /// delays packets unevenly: two shots sent a refire apart can be handled closer together
        /// than that, or one late and the next on time. A strict clock would refuse the second
        /// of a close pair, and charge a late shot's delay to the one after it. Within the
        /// allowance a shot is charged from when it was due instead, so over any stretch of time
        /// no more than one shot per refire is fired - plus, once, twice the allowance's worth.
        /// </summary>
        private const long ShotTolerance = 250;

        /// <summary>
        /// The least a shot is charged, in ms. The auto-fire list is walked once every 100 ms
        /// (MapChannelManager's "AutoFire" timer), which is the fastest the server fires a weapon
        /// by itself; a weapon whose refire reads 0 is held to that rather than to nothing.
        /// </summary>
        private const long MinRefire = 100;

        private enum FireResult
        {
            Fired,
            /// <summary>Everything else allowed the shot, and the shot clock did not yet.</summary>
            TooSoon,
            NotFired
        }

        public static byte MaxPlayerLevel = 50;

        /// <summary>The levels that award a clone credit, per the live game's own rules.</summary>
        public static readonly byte[] CloneCreditLevels = { 5, 15, 30 };
        public static ManifestationManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new ManifestationManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private ManifestationManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        // constant skillId data
        public readonly int[] SkillIById = {
            1,8,14,19,20,21,22,23,24,
            25,26,28,30,31,32,34,35,
            36,37,39,40,43,47,48,49,
            50,54,55,57,58,63,66,67,
            68,72,73,77,79,80,82,89,
            92,102,110,111,113,114,121,135,
            136,147,148,149,150,151,152,153,
            154,155,156,157,158,159,160,161,
            162,163,164,165,166,172,173,174
        };
        // table for skillId to skillIndex mapping
        private readonly int[] SkillId2Idx =
        {
            -1,0,-1,-1,-1,-1,-1,-1,1,-1,-1,-1,-1,-1,2,-1,-1,-1,-1,3,
            4,5,6,7,8,9,10,-1,11,-1,12,13,14,-1,15,16,17,18,-1,19,
            20,-1,-1,21,-1,-1,-1,22,23,24,25,-1,-1,-1,26,27,-1,28,29,-1,
            -1,-1,-1,30,-1,-1,31,32,33,-1,-1,-1,34,35,-1,-1,-1,36,-1,37,
            38,-1,39,-1,-1,-1,-1,-1,-1,40,-1,-1,41,-1,-1,-1,-1,-1,-1,-1,
            -1,-1,42,-1,-1,-1,-1,-1,-1,-1,43,44,-1,45,46,-1,-1,-1,-1,-1,
            -1,47,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,48,49,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,50,51,52,53,54,55,56,57,58,59,60,61,62,
            63,64,65,66,67,68,69,-1,-1,-1,-1,-1,70,71,72,-1,-1,-1,-1,-1,
            -1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1,-1
        };
        // table for skillIndex to ability mapping
        public readonly int[] SkillIdx2AbilityId =
        {
            -1, -1, -1, -1, 137, -1, -1, -1, -1, 178, 177, 158, -1, -1,
            197, 186, 188, 162, 187, -1, -1, 233, 234, -1, 194, -1, -1,
            -1, -1, -1, 301, -1, -1, 185, 251, 240, 302, 232, 229, -1,
            231, 305, 392, 252, 282, 381, 267, 298, 246, 253, 307, 393,
            281, 390, 295, 304, 386, 193, 385, 176, 260, 384, 383, 303,
            388, 389, 387, 380, 401, 430, 262, 421, 446
        };

        /// <summary>
        /// Points to reach each skill rank, cumulative. The index is the rank, so the array's
        /// last index is the cap.
        /// </summary>
        public readonly int[] requiredSkillLevelPoints = { 0, 1, 3, 6, 10, 15 };

        /// <summary>The highest rank any skill goes to, which is what the client's own UI caps at.</summary>
        public const int MaxSkillLevel = 5;

        #region Handlers
        public void AutoFireKeepAlive(Client client, int keepAliveDelay)
        {
            // Four times the client's own interval, within reason: the value is the client's,
            // and zero or negative would stop the fire on the next tick, huge would keep it
            // going for a crashed client.
            var aliveTime = Math.Clamp((long)keepAliveDelay * 4, 1000, 30000);

            foreach (var timer in AutoFire)
                if (timer.Client == client)
                    timer.MaxAliveTime = aliveTime;
        }

        public void ChangeShowHelmet(Client client, ChangeShowHelmetPacket packet)
        {
            Logger.WriteLog(LogType.Debug, "ToDo ChangeShowHelmet");
        }

        public void ChangeTitle(Client client, uint titleId)
        {
            //if (titleId != 0)
            //{
            client.Player.CurrentTitle = titleId;
            client.CallMethod(client.Player.EntityId, new TitleChangedPacket(titleId));
            /*}
            else
            {
                client.SendPacket(client.MapClient.Player.Actor.EntityId, new TitleRemovedPacket(client.MapClient.Player.CurrentTitle));
                client.MapClient.Player.CurrentTitle = titleId;
            }

            client.MapClient.Player.CurentTitle = titleId;*/
        }

        public bool PlayerTryFireWeapon(Client client) => TryFireWeapon(client) == FireResult.Fired;

        /// <summary>
        /// Fires the weapon in hand if it can be fired now. Every shot a player makes comes through
        /// here, by three routes: the auto-fire list, the first shot of StartAutoFire, and
        /// RequestWeaponAttack.
        /// </summary>
        private FireResult TryFireWeapon(Client client)
        {
            // Reached from the auto-fire list on the main loop as well as from the handler; a
            // client that has left the world since must not be fired for.
            if (client.Player == null || client.State != ClientState.Ingame)
                return FireResult.NotFired;

            var weapon = InventoryManager.Instance.CurrentWeapon(client);

            // Nothing in hand fires nothing. Tested before the draw below, because an empty
            // hand and a stowed weapon look the same from WeaponReady: arming an empty drawer
            // slot clears both the weapon and WeaponReady, so a player still holding the
            // trigger reached the draw with no weapon to describe it, and the auto-fire list
            // is walked at the top of the map channel worker - the dereference took the whole
            // tick with it, on every map, for as long as the client kept the fire alive.
            if (weapon == null)
                return FireResult.NotFired;

            // A jammed weapon does nothing until it is reloaded. Checked before WeaponReady so
            // that a jam does not get mistaken for a weapon that is merely stowed and silently
            // drawn instead.
            if (weapon.IsJammed)
            {
                // Once per trigger pull would be once per tick while auto-fire is held, so the
                // message is not repeated - the client already showed it when the jam arrived,
                // and its ammo readout still says "Jammed".
                return FireResult.NotFired;
            }

            // ToDo: isOverheated, and some other checks
            if (!client.Player.WeaponReady)
            {
                RequestWeaponDraw(client);
                return FireResult.NotFired;
            }

            var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(weapon);

            if (weaponClassInfo == null)
                return FireResult.NotFired;

            // A weapon being reloaded does not fire. The client never asks it to: primary fire
            // waits for a reload to finish, and anything else the player does interrupts the
            // reload first - RequestActionInterrupt, then the attack, handled in that order - so
            // by the time a legitimate shot is looked at here the reload is already marked. A
            // shot with the reload still live fired out of the clip while the reload went on to
            // top the clip up anyway, so a reload never kept anyone from firing.
            if (IsReloading(client.Player))
                return FireResult.NotFired;

            // do we need to reload?
            if (weapon.CurrentAmmo < weapon.ItemTemplate.WeaponInfo.AmmoPerShot)
            {
                RequestWeaponReload(client, true);
                return FireResult.NotFired;
            }

            // The shot clock. Nothing used to time shots at all: RequestWeaponAttack fired as
            // often as it arrived, a clip per tick if the client sent a clip's worth, and every
            // StartAutoFire fired at once whatever the last shot had been, so pressing fire over
            // and over outran holding it down. Last of the checks, so that TooSoon always means a
            // shot that would otherwise have gone.
            //
            // The refire is the weapon's own, and the same one the auto-fire timer waits between
            // shots, so a player cannot make a weapon fire faster than the server fires it itself.
            var now = Environment.TickCount64;

            if (ShotWait(client.Player, now) > 0)
                return FireResult.TooSoon;

            // The next shot is due a refire after this one was due - or, if this one came more than
            // the allowance late, a refire after it came less the allowance. Charged before
            // anything below can throw, so a shot that fails half way is still a shot as far as
            // the clock is concerned.
            client.Player.NextShotAt = Math.Max(client.Player.NextShotAt, now - ShotTolerance) + Math.Max(MinRefire, weapon.ItemTemplate.WeaponInfo.Refire);

            // decrease ammo count
            weapon.CurrentAmmo -= weapon.ItemTemplate.WeaponInfo.AmmoPerShot;
            client.CallMethod(weapon.EntityId, new WeaponAmmoInfoPacket(weapon.CurrentAmmo));

            // Written per shot, and deliberately so: RemovePlayer destroys the inventory on every
            // map change and MapLoaded reads it back from the database, so a clip count left to be
            // written later would come back from any zone change it was not flushed before with
            // the rounds already fired still in it. The shot clock above is what keeps this write
            // to the rate the weapon fires at.
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.Items.UpdateAmmo(weapon);

            // The barrel gets hotter. Done after the shot has been paid for in ammo, so a shot
            // that did not happen does not heat anything, and before the missile, so a shot that
            // reaches capacity is still fired - the jam stops the next one, not this one.
            AddWeaponHeat(client, weapon);

            // let's calculate damage
            var damageRange = weaponClassInfo.MaxDamage - weaponClassInfo.MinDamage;
            var damage = weaponClassInfo.MinDamage + new Random().Next(0, damageRange + 1);
            var action = new ActionData(client.Player, weaponClassInfo.WeaponAttackActionId, weaponClassInfo.WeaponAttackArgId, client.Player.Target, 0);
            // launch correct missile type depending on weapon type
            MissileManager.Instance.MissileLaunch(client.Player.MapChannel, action, damage);
            
            return FireResult.Fired;
        }

        /// <summary>
        /// How long before the player's next shot may be fired, in ms; 0 when it may be fired now.
        /// A shot is allowed <see cref="ShotTolerance"/> ahead of <see cref="Manifestation.NextShotAt"/>.
        /// </summary>
        private static long ShotWait(Manifestation player, long now)
        {
            return Math.Max(0, player.NextShotAt - ShotTolerance - now);
        }

        /// <summary>
        /// Whether the player has a reload waiting out its reload time that has not been
        /// interrupted. An interrupted one stays in the queue until the next tick sees to it, and
        /// is already over as far as the player is concerned.
        /// </summary>
        private static bool IsReloading(Manifestation player)
        {
            var queue = player.MapChannel?.PerformRecovery;

            if (queue == null)
                return false;

            foreach (var action in queue)
                if (action.Actor == player && action.ActionId == ActionId.WeaponReload && !action.IsInrerrupted)
                    return true;

            return false;
        }

        /// <summary>
        /// This weapon's heat, brought up to date.
        ///
        /// Cooling is applied on read rather than on a timer, which is exactly what the client
        /// does - <c>_UpdateWeaponHeat</c> cools by the elapsed time whenever the value is
        /// touched. Nothing needs to tick, and a weapon nobody is firing costs nothing.
        /// </summary>
        public double CurrentHeat(Item weapon)
        {
            if (weapon?.ItemTemplate?.WeaponInfo == null)
                return 0;

            var now = Environment.TickCount64;

            if (weapon.HeatUpdatedAt == 0)
            {
                weapon.HeatUpdatedAt = now;
                return weapon.Heat;
            }

            var cooled = weapon.Heat - WeaponHeat.Cooling(weapon.ItemTemplate.WeaponInfo.CoolRate, now - weapon.HeatUpdatedAt);

            weapon.Heat = cooled < 0 ? 0 : cooled;
            weapon.HeatUpdatedAt = now;

            return weapon.Heat;
        }

        /// <summary>
        /// Adds one shot's worth of heat, and jams the weapon if that reaches capacity.
        ///
        /// A worn weapon heats faster: the client scales the shot by
        /// <c>(100 + (100 - condition)) / 100</c>, so a weapon at half condition heats at one and
        /// a half times the rate and one at zero at double. That is the client's own arithmetic,
        /// and this matches it so the heat meter the player is watching agrees with the jam they
        /// get.
        /// </summary>
        public void AddWeaponHeat(Client client, Item weapon)
        {
            if (weapon?.ItemTemplate?.WeaponInfo == null)
                return;

            var heat = CurrentHeat(weapon) + WeaponHeat.PerShot(weapon.ItemTemplate.WeaponInfo.HeatPerShot, ConditionPercent(weapon));

            weapon.Heat = heat;

            if (heat >= WeaponHeat.Capacity)
                JamWeapon(client, weapon);
        }

        /// <summary>
        /// Jams a weapon and tells its owner.
        ///
        /// Public because overheating is not the only way in: the client's help says "some
        /// creatures have been known to jam weapons", and a creature action that did so would
        /// call this. Nothing does yet - no row in creature_action identifies itself as a jamming
        /// attack, and picking one would be a guess - so this is the hook and not the feature.
        /// </summary>
        public void JamWeapon(Client client, Item weapon)
        {
            if (client?.Player == null || weapon == null || weapon.IsJammed)
                return;

            weapon.IsJammed = true;

            // Pinned at capacity rather than left to drift above it, so that cooling from a jam
            // always starts from the same place however far past the line the shot went.
            weapon.Heat = WeaponHeat.Capacity;
            weapon.HeatUpdatedAt = Environment.TickCount64;

            client.CallMethod(weapon.EntityId, new WeaponJammedPacket(true));
        }

        /// <summary>
        /// Frees a jammed weapon. Reloading is the only way a player has to do this, which is why
        /// the reload path lets a jammed weapon through checks that would otherwise refuse it.
        /// </summary>
        public void ClearJam(Client client, Item weapon)
        {
            if (client?.Player == null || weapon == null || !weapon.IsJammed)
                return;

            weapon.IsJammed = false;

            // Cleared, not merely below the line: leaving the barrel full would jam again on the
            // first shot after the reload.
            weapon.Heat = 0;
            weapon.HeatUpdatedAt = Environment.TickCount64;

            client.CallMethod(weapon.EntityId, new WeaponJammedPacket(false));
        }

        /// <summary>An item's hit points as a percentage of its class maximum, the way the client reads condition.</summary>
        private static double ConditionPercent(Item item)
        {
            var classInfo = EntityClassManager.Instance.GetClassInfo(item.ItemTemplate.Class);
            var max = classInfo?.ItemClassInfo?.MaxHitPoints ?? 0;

            if (max <= 0)
                return 100;

            return 100.0 * item.CurrentHitPoints / max;
        }

        // ------------------------------------------------------------------ combat state

        /// <summary>
        /// Puts the player in combat, or extends the time they stay there. Called from both ends
        /// of a damage event - dealing it and taking it - because either is being in a fight.
        /// </summary>
        public void EnterCombat(Client client)
        {
            if (client?.Player == null || client.State != ClientState.Ingame)
                return;

            client.Player.CombatExpiresAt = Environment.TickCount64 + CombatRegen.CombatTimeoutMs;

            if (client.Player.InCombat)
                return;

            client.Player.InCombat = true;

            ApplyRegenPeriod(client.Player);

            client.CallMethod(client.Player.EntityId, new PlayerEnteredCombatPacket());

            // The rate change is not in that packet - it carries nothing - so the attributes go
            // too. AttributeInfo rather than UpdateAttributes because only AttributeInfo carries
            // refreshPeriod, which is the field the modifier moves.
            client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
        }

        /// <summary>Takes the player out of combat and restores their regeneration.</summary>
        public void ExitCombat(Client client)
        {
            if (client?.Player == null || !client.Player.InCombat)
                return;

            client.Player.InCombat = false;
            client.Player.CombatExpiresAt = 0;

            ApplyRegenPeriod(client.Player);

            if (client.State != ClientState.Ingame)
                return;

            client.CallMethod(client.Player.EntityId, new PlayerExitedCombatPacket());
            client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
        }

        /// <summary>
        /// Sets the health and armour refresh periods for the player's current combat state.
        ///
        /// The period rather than the amount, because both are integers on the wire and a base
        /// amount of 2 scaled by 0.2 truncates to nothing. See <see cref="CombatRegen"/>.
        ///
        /// This is the only place either period is set, including the out-of-combat value, so
        /// that there is one answer to what the period is rather than two that have to agree. It
        /// is called at the end of UpdateStatsValues for that reason and for a second one:
        /// UpdateStatsValues recomputes the rates from scratch, so without it, changing a piece
        /// of armour mid-fight would quietly restore full regeneration.
        ///
        /// A period of zero would stop regeneration entirely - the client's
        /// _EvaluatePredictedRefresh returns early on one - so neither branch may yield it.
        /// </summary>
        public void ApplyRegenPeriod(Manifestation player)
        {
            if (player == null)
                return;

            var period = player.InCombat
                ? CombatRegen.InCombatRegenPeriodSeconds
                : CombatRegen.RegenPeriodSeconds;

            player.Attributes[Attributes.Health].RefreshPeriod = period;
            player.Attributes[Attributes.Armor].RefreshPeriod = period;
        }

        /// <summary>Drops players out of combat once their timer has run out.</summary>
        public void CombatWorker(MapChannel mapChannel)
        {
            var now = Environment.TickCount64;

            foreach (var client in mapChannel.ClientList)
            {
                if (client?.Player == null || !client.Player.InCombat)
                    continue;

                if (now >= client.Player.CombatExpiresAt)
                    ExitCombat(client);
            }
        }

        /// <summary>
        /// Consumes a clone-credit item and gives its owner the credit.
        ///
        /// The client sends this from the item's right-click menu and expects nothing back except
        /// the new total: <c>Recv_CloneCredits</c> raises the "clone credit added" message and the
        /// tutorial itself, but only when the number it is given is higher than the one it had, so
        /// the packet has to go out after the increment and not before.
        ///
        /// Everything is checked here rather than trusted. The client only offers the right-click
        /// on an item with the CloneCredit augmentation, but the entity id arrived over the wire.
        /// </summary>
        public void RequestUseCloneCredit(Client client, RequestUseCloneCreditPacket packet)
        {
            if (client?.Player == null)
                return;

            // Theirs, and in the pack rather than a lockbox or someone else's window.
            if (!client.Player.Inventory.PersonalInventory.Contains(packet.EntityId))
                return;

            var item = EntityManager.Instance.GetItem(packet.EntityId);

            if (item?.ItemTemplate == null)
                return;

            var classInfo = EntityClassManager.Instance.GetClassInfo(item.ItemTemplate.Class);

            // The augmentation is what makes an item a clone credit - not its template id, so a
            // second one added later works without touching this.
            if (classInfo == null || !classInfo.Augmentations.Contains(AugmentationType.CloneCredit))
            {
                Logger.WriteLog(LogType.Error,
                    $"RequestUseCloneCredit: {client.Player.Name} used item {packet.EntityId} (class {item.ItemTemplate.Class}), which is not a clone credit");
                return;
            }

            if (item.StackSize == 0)
                return;

            // Spent before it is granted, so that a failure here cannot mint a credit from
            // nothing. ReduceStackCount's own preconditions - a live player, a non-null item, a
            // non-zero count, and the item being in the named inventory - are all established
            // above, so once it is reached it consumes.
            //
            // Worth knowing which branch it takes, because only one of them touches StackSize: a
            // stack of several is decremented, while the last of a stack is destroyed and
            // unregistered with its count left alone. Anything downstream that wants to know
            // whether an item is gone has to look at the inventory slot, not the number.
            InventoryManager.Instance.ReduceStackCount(client, InventoryType.Personal, item, 1);

            client.Player.CloneCredits++;
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.CloneCredits);
            client.CallMethod(client.Player.EntityId, new CloneCreditsPacket(client.Player.CloneCredits));
        }

        public void RequestArmAbility(Client client, int abilityDrawerSlot)
        {
            client.Player.CurrentAbilityDrawer = abilityDrawerSlot;
            // ToDo do we need upate Database???
            client.CallMethod(client.Player.EntityId, new AbilityDrawerSlotPacket(abilityDrawerSlot));
        }

        public void RequestArmWeapon(Client client, uint requestedWeaponDrawerSlot)
        {
            // The drawer has five slots; the index came straight from the client.
            if (client.Player == null || requestedWeaponDrawerSlot >= client.Player.Inventory.WeaponDrawer.Count)
                return;

            client.Player.ActiveWeapon = (byte)requestedWeaponDrawerSlot;

            client.CallMethod(client.Player.EntityId, new WeaponDrawerSlotPacket(requestedWeaponDrawerSlot, true));

            var weapon = EntityManager.Instance.GetItem(client.Player.Inventory.WeaponDrawer[client.Player.ActiveWeapon]);

            // A drawer slot holding something that is not a weapon is armed as an empty one.
            // RequestEquipWeapon refuses to put anything else there now, but a drawer loaded
            // before it did still has to be survivable: arming that slot used to ask for an
            // appearance the class has none of and disconnect the player, which made the slot a
            // trap they could not clear from in front of it.
            if (weapon != null && EntityClassManager.Instance.GetEquipableClassInfo(weapon)?.EquipmentSlotId != EquipmentData.Weapon)
            {
                Logger.WriteLog(LogType.Error,
                    $"{client.Player.Name} has {weapon.ItemTemplate.Class} in weapon drawer slot {requestedWeaponDrawerSlot}, which is not a weapon; armed as empty.");

                weapon = null;
            }

            // The weapon in hand follows the active slot, empty included: arming an empty slot
            // used to leave the previous weapon in EquippedInventory[13], so the player kept
            // firing a weapon they had put away.
            client.Player.Inventory.EquippedInventory[13] = weapon?.EntityId ?? 0;

            if (weapon == null)
            {
                if (client.Player.WeaponReady)
                    WeaponReady(client, false);

                CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.ActiveWeapon, (byte)requestedWeaponDrawerSlot);
                return;
            }

            NotifyEquipmentUpdate(client);
            SetAppearanceItem(client, weapon);
            UpdateAppearance(client);
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.ActiveWeapon, (byte)requestedWeaponDrawerSlot);
            // update ammo info
            client.CallMethod(weapon.EntityId, new WeaponAmmoInfoPacket(weapon.CurrentAmmo));
        }

        public void RequestSetAbilitySlot(Client client, RequestSetAbilitySlotPacket packet)
        {
            // todo: do we need to check if ability is available ??
            if (packet.AbilityId == 0)
            {
                // remove ability is used
                client.Player.Abilities.Remove(packet.SlotId);
            }
            else
            {
                // added new ability
                client.Player.Abilities.TryGetValue(packet.SlotId, out AbilityDrawerData ability);
                if (ability == null)
                {
                    client.Player.Abilities.Add(packet.SlotId, new AbilityDrawerData(packet.SlotId, (int)packet.AbilityId, (uint)packet.AbilityLevel));
                }
                else
                {
                    client.Player.Abilities[packet.SlotId].AbilityId = (int)packet.AbilityId;
                    client.Player.Abilities[packet.SlotId].AbilityLevel = (uint)packet.AbilityLevel;
                    client.Player.Abilities[packet.SlotId].AbilitySlotId = packet.SlotId;
                }
            }
            // update database with new drawer slot ability
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterAbilityDrawers.AddOrUpdate(client.Player.Id, packet.SlotId, (int)packet.AbilityId, (uint)packet.AbilityLevel);
            // send packet
            client.CallMethod(client.Player.EntityId, new AbilityDrawerPacket(client.Player.Abilities));
        }

        public void RequestSwapAbilitySlots(Client client, RequestSwapAbilitySlotsPacket packet)
        {
            AbilityDrawerData toSlot;
            var abilities = client.Player.Abilities;
            var fromSlot = abilities[packet.FromSlot];
            abilities.TryGetValue(packet.ToSlot, out toSlot);
            if (toSlot == null)
            {
                abilities.Add(packet.ToSlot, new AbilityDrawerData(packet.ToSlot, fromSlot.AbilityId, fromSlot.AbilityLevel));
                abilities.Remove(packet.FromSlot);
            }
            else
            {
                abilities[packet.ToSlot] = abilities[packet.FromSlot];
                abilities[packet.ToSlot].AbilitySlotId = packet.ToSlot;
                abilities[packet.FromSlot] = toSlot;
                abilities[packet.FromSlot].AbilitySlotId = packet.FromSlot;
            }
            // Do we need to update database here ???
            // update database with new drawer slot ability
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterAbilityDrawers.AddOrUpdate(
                client.Player.Id,
                abilities[packet.ToSlot].AbilitySlotId,
                abilities[packet.ToSlot].AbilityId,
                abilities[packet.ToSlot].AbilityLevel);
            // check if fromSlot isn't empty now
            abilities.TryGetValue(packet.FromSlot, out AbilityDrawerData tempSlot);
            if (tempSlot != null)
                unitOfWork.CharacterAbilityDrawers.AddOrUpdate(
                    client.Player.Id,
                    abilities[packet.FromSlot].AbilitySlotId,
                    abilities[packet.FromSlot].AbilityId,
                    abilities[packet.FromSlot].AbilityLevel);
            else
                unitOfWork.CharacterAbilityDrawers.AddOrUpdate(client.Player.Id, packet.FromSlot, 0, 0);
            // send packet
            client.CallMethod(client.Player.EntityId, new AbilityDrawerPacket(abilities));
        }

        public void StartAutoFire(Client client, double yaw)
        {
            // ToDo:
            // yaw is probobly used to mach player and target orientation,
            // some creatures recive more damage from back then from front

            switch (TryFireWeapon(client))
            {
                case FireResult.Fired:
                    ActorManager.Instance.RequestVisualCombatMode(client, true);
                    RegisterAutoFire(client);
                    break;

                // Pressed again before the last shot's refire was up. The press still starts the
                // fire, from when the clock allows: only a first shot that went used to start the
                // timer, so a first shot held back would leave the trigger down and nothing firing
                // until the player let go and pressed again.
                case FireResult.TooSoon:
                    if (RegisterAutoFire(client, ShotWait(client.Player, Environment.TickCount64)))
                        ActorManager.Instance.RequestVisualCombatMode(client, true);
                    break;
            }
        }

        public void StopAutoFire(Client client)
        {
            ActorManager.Instance.RequestVisualCombatMode(client, false);

            RemoveAutoFire(client);
        }

        #endregion

        #region Helper Functions

        public void AllocateAttributePoints(Client client, AllocateAttributePointsPacket packet)
        {
            // The three counts are the client's word, and used to be added as they came: no
            // check against the points the character has actually earned, and no check for a
            // negative that would take spent points back. Health and armour are derived from
            // the spent points and written to the row, so one packet with Body = 100000 was a
            // permanent giant health pool.
            var available = GetAvailableAttributePoints(client.Player);
            var requested = (long) packet.Body + packet.Mind + packet.Spirit;

            if (packet.Body < 0 || packet.Mind < 0 || packet.Spirit < 0 || requested <= 0 || requested > available)
            {
                Logger.WriteLog(LogType.Security,
                    $"AccountId = {client.AccountEntry.Id} tried to allocate {packet.Body}/{packet.Mind}/{packet.Spirit} attribute points with {available} available.");

                // Whatever the client's window thinks, this is where the character stands.
                client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
                return;
            }

            client.Player.SpentBody += packet.Body;
            client.Player.SpentMind += packet.Mind;
            client.Player.SpentSpirit += packet.Spirit;

            UpdateStatsValues(client, false);

            // update DB
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Attributes, null);

            // Send Data to client
            client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
        }

        public void AssignPlayer(Client client)
        {
            var player = client.Player;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            // get charaterOptions. Cleared first: this runs again on every map change, and the
            // list used to gain another copy of every option each time.
            player.CharacterOptions.Clear();

            var optionsList = unitOfWork.CharacterOptions.Get(player.Id);

            foreach (var characterOption in optionsList)
                player.CharacterOptions.Add(new CharacterOptions((CharacterOption)characterOption.OptionId, characterOption.Value));

            client.CallMethod(SysEntity.ClientMethodId, new CharacterOptionsPacket(player.CharacterOptions));

            client.CallMethod(SysEntity.ClientMethodId, new SetControlledActorIdPacket(player.EntityId));

            client.CallMethod(player.EntityId, new WeaponDrawerSlotPacket(player.ActiveWeapon, false));

            client.CallMethod(SysEntity.ClientGameMapId, new SetSkyTimePacket { RunningTime = 6666666 });   // ToDo add actual time how long map is running

            client.CallMethod(SysEntity.ClientMethodId, new SetCurrentContextIdPacket(client.Player.MapChannel.MapInfo.MapContextId));

            SocialManager.Instance.SetSocialContactList(client);

            client.CallMethod(player.EntityId, new ActorInfoPacket(player));

            // The regions the player is standing in; re-sent by RegionManager.Worker as they move.
            RegionManager.Instance.PlayerEnteredMap(client);
            MapMarkerManager.Instance.PlayerEnteredMap(client);

            client.CallMethod(player.EntityId, new AdvancementStatsPacket(
                player.Level,
                player.Experience,
                GetAvailableAttributePoints(player),
                0,       // trainPoints (are not used by the client??)
                GetSkillPointsAvailable(player)
            ));

            client.CallMethod(player.EntityId, new SkillsPacket(player.Skills));

            client.CallMethod(player.EntityId, new AbilitiesPacket(player.Skills));

            // don't send this packet if abilityDrawer is empty
            if (player.Abilities.Count > 0)
                client.CallMethod(player.EntityId, new AbilityDrawerPacket(player.Abilities));

            client.CallMethod(player.EntityId, new TitlesPacket(player.Titles));

            client.CallMethod(player.EntityId, new UpdateAttributesPacket(player.Attributes, 0));

            client.CallMethod(player.EntityId, new UpdateHealthPacket(player.Attributes[Attributes.Health], 0));

            client.CallMethod(player.EntityId, new LogosStoneTabulaPacket(player.Logos));

            client.CallMethod(player.EntityId, new AllCreditsPacket(player.Credits));

            client.CallMethod(player.EntityId, new LockboxFundsPacket(player.LockboxCredits));
        }

        public void AutoFireTimerDoWork(long delta)
        {
            // go backwards through list
            for (var i = AutoFire.Count - 1; i >= 0; i--)
            {
                var timer = AutoFire[i];
                // we dont want to server keep fireing if client crash 
                timer.MaxAliveTime -= delta;

                // Nothing in hand ends the fire here rather than in each of the several places
                // that can empty it - arming an empty drawer slot, moving the weapon out of the
                // armed slot, dropping or selling it. The trigger is still held, but there is no
                // longer a weapon to pull it on, and a timer kept for one cannot be fired
                // without asking what it is.
                if (timer.MaxAliveTime <= 0
                    || timer.Client.Player == null
                    || timer.Client.State != ClientState.Ingame
                    || InventoryManager.Instance.CurrentWeapon(timer.Client) == null)
                {
                    AutoFire.RemoveAt(i);
                    continue;
                }

                timer.Delay -= delta;

                if (timer.Delay <= 0)
                {
                    // This list is walked at the top of the map channel worker, before any map
                    // is touched. A shot that throws used to abandon the whole tick - every
                    // map's queued actions, missiles, creature behaviour and visibility - and
                    // the delay below never being reached meant the same client threw again on
                    // the very next tick, so one player could hold the world still. The timer
                    // that could not be fired is dropped instead, and costs only itself.
                    try
                    {
                        PlayerTryFireWeapon(timer.Client);
                    }
                    catch (Exception e)
                    {
                        AutoFire.RemoveAt(i);

                        Logger.WriteLog(LogType.Error, $"Auto-fire for entity {timer.Client.Player?.EntityId} threw and was stopped: {e}");

                        continue;
                    }

                    timer.Delay = timer.RefireTime;
                }
            }
        }

        public void CellDiscardClientToPlayers(Client client, List<Client> notifyClients)
        {
            foreach (var tempClient in notifyClients)
            {
                if (tempClient == client)
                    continue;

                tempClient.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(client.Player.EntityId));
            }
        }

        public void CellDiscardPlayersToClient(Client client, List<Client> notifyClients)
        {
            foreach (var tempClient in notifyClients)
            {
                if (tempClient == null)
                    continue;

                if (tempClient == client)
                    continue;

                client.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(tempClient.Player.EntityId));
            }

        }

        public void CellIntroduceClientToPlayers(Client client, List<Client> clientList)
        {
            var player = client.Player;

            foreach (var tempClient in clientList)
            {
                // don't send data about yourself
                if (tempClient == client)
                    continue;

                tempClient.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(player.EntityId, player.EntityClass, CreatePlayerEntityData(client)));

            }
        }

        public void CellIntroduceClientToSefl(Client client)
        {
            client.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(client.Player.EntityId, client.Player.EntityClass, CreatePlayerEntityData(client)));
        }

        public void CellIntroducePlayersToClient(Client client, List<Client> clientList)
        {
            foreach (var tempClient in clientList)
            {
                if (tempClient == null)
                    continue;

                if (tempClient == client)
                    continue;

                client.CallMethod(SysEntity.ClientMethodId, new CreatePhysicalEntityPacket(tempClient.Player.EntityId, tempClient.Player.EntityClass, CreatePlayerEntityData(tempClient)));
            }
        }
		
		public List<PythonPacket> CreatePlayerEntityData(Client client)
        {
            var player = client.Player;

            var entityData = new List<PythonPacket>
            {
                // PhysicalEntity
                new IsTargetablePacket(EntityClassManager.Instance.GetClassInfo(player.EntityClass).TargetFlag),
                new WorldLocationDescriptorPacket(player.Position, player.Rotation),
                // Manifestation
                new CurrentCharacterIdPacket(player.EntityId),
                new CharacterClassPacket(player.Class),
                new AttributeInfoPacket(player.Attributes),
                new PreloadDataPacket(client.Player.Inventory.EquippedInventory[13], player.Abilities),
                new AppearanceDataPacket(player.AppearanceData),
                new ResistanceDataPacket(player.ResistanceData),
                new ActorControllerInfoPacket(true),
                new LevelPacket(player.Level),
                new CharacterNamePacket(player.Name),
                new ActorNamePacket(player.FamilyName),
                new IsRunningPacket(player.IsRunning),
                new TargetCategoryPacket(Factions.AFS),
                new PlayerFlagsPacket(),
                new IsTrialAccountPacket(player.IsTrialAccount),
                new EquipmentInfoPacket(client.Player.Inventory.EquippedInventory)
            };

            return entityData;
        }

        internal void GainExperience(Client client, uint experience)
        {
            if (client.Player.Level >= MaxPlayerLevel)
                return; // cannot gain xp over level 50

            client.Player.Experience += experience;

            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Expirience, client.Player.Experience);

            var xpInfo = new XPInfo(client.Player.Experience, experience, experience);

            client.CallMethod(client.Player.EntityId, new ExperienceChangedPacket(xpInfo));

            var levelBefore = client.Player.Level;

            // check for level up
            while (client.Player.Level < MaxPlayerLevel)
            {
                var xpForLevelUp = GetLevelNeededExperience(client.Player.Level);

                if (xpForLevelUp == -1)
                    break;

                if (client.Player.Experience >= xpForLevelUp)
                {
                    // level up
                    client.Player.Level++;

                    // A clone credit at 5, 15 and 30. Targets of Opportunity were the other
                    // source in the live game; those are not implemented, so these three are the
                    // whole supply - and without them the clone button at character selection,
                    // which the client greys out at zero credits, can never be pressed.
                    if (Array.IndexOf(CloneCreditLevels, client.Player.Level) >= 0)
                    {
                        client.Player.CloneCredits++;
                        CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.CloneCredits);
                        client.CallMethod(client.Player.EntityId, new CloneCreditsPacket(client.Player.CloneCredits));
                    }

                    // update database
                    CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Level);

                    // Everyone in range, not just the player: actor.Recv_LevelUp calls
                    // SetExperienceLevel on whichever actor it arrived for, so this is what
                    // moves the level shown over someone's head. It guards the fanfare itself -
                    // the tutorial popup and ACTOR_LEVEL_UP event fire only when the entity id
                    // is the receiver's own manifestation - so onlookers just see the number.
                    client.CellCallMethod(client, client.Player.EntityId, new LevelUpPacket(client.Player.Level));

                    var msgArg = new Dictionary<string, string>
                    {
                        { "level", client.Player.Level.ToString() },
                        { "attributePts", GetAvailableAttributePoints(client.Player).ToString() },  // todo: send correct number of new attribute points
                        { "skillPts", GetSkillPointsAvailable(client.Player).ToString() }           // todo: send correct number of new skill points
                    };

                    client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmLevelIncreased, msgArg, MsgFilterId.LeveledUp));

                    // update stats
                    UpdateStatsValues(client, true);
                    client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
                    SendAvailableAllocationPoints(client);
                }
                else
                    break;
            }

            // Once, after the loop: enough experience for two levels at once is one change as
            // far as the squad window is concerned.
            if (client.Player.Level != levelBefore)
                PartyManager.Instance.MemberInfoChanged(client);
        }

        /// <summary>
        /// Adrenaline (chi) earned for a kill, as a percent of the bar. Not a live-game figure:
        /// the client says only that adrenaline is "gained by defeating enemies" and that the
        /// Regen stat "improves ... Adrenaline gain", so the shape - a share of the bar per
        /// kill, scaled by Regen - is the client's and the number is a placeholder to tune.
        /// Five kills at 100% Regen fill an empty bar; sprint then runs for about a minute.
        /// </summary>
        public const int AdrenalinePerKillPercent = 20;

        /// <summary>
        /// Adds adrenaline to the player's bar, up to its maximum, and tells the client. whoId 0
        /// makes the client announce the change (the floating number over the bar), as the live
        /// server did from the killing blow.
        /// </summary>
        internal void GainAdrenaline(Client client, int amount)
        {
            var player = client.Player;

            if (player == null || player.State == CharacterState.Dead || amount <= 0)
                return;

            if (!player.Attributes.TryGetValue(Attributes.Chi, out var chi) || chi.Current >= chi.CurrentMax)
                return;

            chi.Current = Math.Min(chi.CurrentMax, chi.Current + amount);

            client.CallMethod(player.EntityId, new UpdateChiPacket(chi, 0));
        }

        /// <summary>The adrenaline one kill is worth to this player: AdrenalinePerKillPercent of the bar, scaled by Regen.</summary>
        internal int AdrenalineForKill(Client client)
        {
            var player = client.Player;

            if (player == null || !player.Attributes.TryGetValue(Attributes.Chi, out var chi))
                return 0;

            var regenPercent = player.Attributes.TryGetValue(Attributes.Regen, out var regen) ? regen.CurrentMax : 100;

            return (int)Math.Round(chi.CurrentMax * AdrenalinePerKillPercent / 100D * regenPercent / 100D);
        }

        public void DebugChgPlayerClass(Client client, uint newClassId)
        {
            client.Player.Class = newClassId;
            client.CallMethod(client.Player.EntityId, new CharacterClassPacket(client.Player.Class));
            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Class, client.Player.Class);

            // Class is the third field of the same party tuple, so it goes stale the same way.
            PartyManager.Instance.MemberInfoChanged(client);
        }

        /// <summary>
        /// Puts <paramref name="credits"/> into the player's purse and tells them they received it.
        /// The amount is positive and is added.
        /// </summary>
        /// <remarks>
        /// These two were the same unsigned update under different names - neither looked at the
        /// sign, so the direction was decided entirely at the call site and every charge had to
        /// remember to negate. <see cref="InventoryManager.PurchaseLockboxTab"/> believed the name
        /// instead, so the four lockbox tabs paid the player 100 K to 100 M credits each.
        /// The names mean what they say now, and the sign is not the caller's to choose.
        /// </remarks>
        public void GainCredits(Client client, int credits)
        {
            if (credits <= 0)
            {
                Logger.WriteLog(LogType.Error, $"GainCredits({credits}) for {client.Player?.FamilyName}: credits are gained in positive amounts. Nothing moved.");
                return;
            }

            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, credits);
            // send player message
            client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmGotMoneyLootFromUnknown, new Dictionary<string, string> { { "amount", credits.ToString() } }, MsgFilterId.LootObtained));
        }

        /// <summary>
        /// Takes <paramref name="credits"/> out of the player's purse. The amount is positive and
        /// is subtracted. Returns false, having moved nothing, if they cannot pay - so a caller
        /// that forgets its own funds check refuses the purchase rather than running up a debt.
        /// </summary>
        public bool LossCredits(Client client, int credits)
        {
            if (credits < 0)
            {
                Logger.WriteLog(LogType.Error, $"LossCredits({credits}) for {client.Player?.FamilyName}: charges are positive amounts. Nothing moved.");
                return false;
            }

            if (credits == 0)
                return true;

            if (client.Player.Credits[CurencyType.Credits] < credits)
                return false;

            CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, -credits);
            return true;
        }

        public int GetAvailableAttributePoints(Manifestation player)
        {
            var points = 3 * (player.Level - 1);
            points -= player.SpentBody;
            points -= player.SpentMind;
            points -= player.SpentSpirit;
            //points = Math.Max(points, 0); Probably do not need this? (StaticVariable)
            return points;
        }

        public void GetCustomizationChoices(Client client, GetCustomizationChoicesPacket packet)
        {
            // ToDo
            var test = EntityManager.Instance.GetEntityType(packet.EntityId);
            var testChoices = new Dictionary<int, int>
            {
                { 3663, 36 },
                { 3672, 42 },
                { 3812, 60 }
            };
            client.CallMethod(SysEntity.ClientMethodId, new CustomizationChoicesPacket(packet.EntityId, testChoices));
        }

        private int GetLevelNeededExperience(int level)
        {
            if (level < 1 || level >= 50)
                return -1;

            return ExpPerLevel.ExpRequred[level];
        }

        public int GetSkillIndexById(int skillId)
        {
            return skillId < 0 ? -1 : skillId >= 200 ? -1 : SkillId2Idx[skillId];
        }

        public int GetSkillPointsAvailable(Manifestation player)
        {
            var level = player.Level;

            var pointsAvailable = (player.Level - 1) * 2;
            pointsAvailable += 5; // add five points because of the recruit skills that start at level 1

            if (level >= 5)
                pointsAvailable += 2;

            if (level >= 15)
                pointsAvailable += 2;

            if (level >= 30)
                pointsAvailable += 2;

            if (level >= 50)
                pointsAvailable += 4;

            // subtract spent skill levels
            foreach (var skill in player.Skills)
            {
                var skillLevel = skill.Value.SkillLevel;
                if (skillLevel < 0 || skillLevel > 5)
                    continue; // should not be possible
                pointsAvailable -= requiredSkillLevelPoints[skillLevel];
            }
            return Math.Max(0, pointsAvailable);
        }

        /// <summary>
        /// Which class grants each skill and the level it takes, from the client's own
        /// skillCharacter table. Loaded once at startup.
        /// </summary>
        private readonly Dictionary<SkillId, (CharacterClass Class, int Level)> _skillClasses = new();

        public void LoadSkillClasses()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();

            foreach (var entry in unitOfWork.Actions.GetSkillCharacters())
                _skillClasses[(SkillId)entry.Id] = ((CharacterClass)entry.ClassId, (int)entry.RequiredLevel);

            Logger.WriteLog(LogType.Initialize, $"Loaded {_skillClasses.Count} skill class requirements.");
        }

        /// <summary>
        /// Why this request may not be honoured, or null if it may.
        ///
        /// The client runs all of this before it will even draw the spend buttons - the skills
        /// window shows them only when IsCharacterClass(skill's class, player's class) holds, and
        /// only up to the levels the player has - but none of it constrains the wire. Without a
        /// copy here, the only cost of training a Tier 4 skill on a level 1 Recruit was the skill
        /// points, and abilities are granted by the skills a player holds: AbilityManager.Owns
        /// walks Player.Skills and asks nothing about how they got there.
        ///
        /// Checked as a whole before anything is written, because the request is a set: a list
        /// that is good up to its fifth entry must not leave the first four trained.
        /// </summary>
        private string ValidateSkillLevels(Client client, LevelSkillsPacket packet)
        {
            if (packet.SkillIds == null || packet.SkillLevels == null)
                return "a request with no skill list at all";

            if (packet.ListLenght < 0 || packet.ListLenght > packet.SkillIds.Length
                                      || packet.ListLenght > packet.SkillLevels.Length)
                return "a list length that does not match the list";

            var playerClass = (CharacterClass)client.Player.Class;
            var seen = new HashSet<SkillId>();
            var pointsAvailable = GetSkillPointsAvailable(client.Player);

            for (var i = 0; i < packet.ListLenght; i++)
            {
                var rawId = packet.SkillIds[i];
                var skillId = (SkillId)rawId;

                // GetSkillIndexById answers -1 for an id outside the table, and the index went
                // straight into SkillIdx2AbilityId - so a skill id of 0, or anything past 199,
                // was an IndexOutOfRangeException out of the handler, which is a disconnect.
                var index = GetSkillIndexById(rawId);

                if (index < 0 || index >= SkillIdx2AbilityId.Length)
                    return $"skill id {rawId}, which is not in the skill table";

                // The same id twice would be counted once against the points and written twice.
                if (!seen.Add(skillId))
                    return $"skill {rawId} twice in one request";

                if (!_skillClasses.TryGetValue(skillId, out var requirement))
                    return $"skill {rawId}, which no class grants";

                // gameuiutil's IsCharacterClass: the skill's class has to be the player's own or
                // one they advanced through. A Commando keeps their Soldier and Recruit skills
                // and can never train a Ranger's.
                if (!CharacterClassTree.Is(playerClass, requirement.Class))
                    return $"skill {rawId}, which belongs to {requirement.Class} and not to {playerClass}";

                if (client.Player.Level < requirement.Level)
                    return $"skill {rawId} at level {client.Player.Level}, which needs {requirement.Level}";

                var newSkillLevel = packet.SkillLevels[i];
                var oldSkillLevel = client.Player.Skills.TryGetValue(skillId, out var held) ? held.SkillLevel : 0;

                if (newSkillLevel < 0 || newSkillLevel > MaxSkillLevel)
                    return $"skill {rawId} at rank {newSkillLevel}";

                // Levelling a skill down is not a refund, it is a way to spend the same points
                // twice - the points come back and the ranks already bought stay.
                if (newSkillLevel < oldSkillLevel)
                    return $"skill {rawId} lowered from {oldSkillLevel} to {newSkillLevel}";

                pointsAvailable -= requiredSkillLevelPoints[newSkillLevel] - requiredSkillLevelPoints[oldSkillLevel];
            }

            if (pointsAvailable < 0)
                return "more skill points than this character has";

            return null;
        }

        public void LevelSkills(Client client, LevelSkillsPacket packet)
        {
            var skillLevelupArray = new Dictionary<SkillId, SkillsData>(); // used to temporarily safe skill level updates
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            // Refused rather than thrown. Every one of these used to be an exception out of a
            // packet handler, which Client.Update catches as a malformed packet and answers by
            // closing the connection - so a client one version out of step, or one repeating a
            // request the player had already spent the points on, was disconnected rather than
            // told no.
            var refusal = ValidateSkillLevels(client, packet);

            if (refusal != null)
            {
                Logger.WriteLog(LogType.Security,
                    $"{client.Player.FamilyName} sent a LevelSkills this character may not have: {refusal}. Ignored.");

                // Their copy of the skills is now ahead of ours; put it back.
                client.CallMethod(client.Player.EntityId, new SkillsPacket(client.Player.Skills));
                SendAvailableAllocationPoints(client);
                return;
            }

            for (var i = 0; i < packet.ListLenght; i++)
            {
                var skillId = (SkillId)packet.SkillIds[i];
                var oldSkillLevel = 0;
                var abilityId = SkillIdx2AbilityId[GetSkillIndexById(packet.SkillIds[i])];

                if (client.Player.Skills.ContainsKey(skillId))
                    oldSkillLevel = client.Player.Skills[skillId].SkillLevel;
                else
                {
                    // create new entry in character skils
                    client.Player.Skills.Add(skillId, new SkillsData(skillId, abilityId, 0));
                }

                var newSkillLevel = packet.SkillLevels[i];

                skillLevelupArray.Add(skillId, new SkillsData(skillId, abilityId, newSkillLevel - oldSkillLevel));
            }
            // everything ok, update skills!
            foreach (var skill in skillLevelupArray)
                client.Player.Skills[skill.Value.SkillId].SkillLevel += skillLevelupArray[skill.Value.SkillId].SkillLevel;
            // send skill update to client
            client.CallMethod(client.Player.EntityId, new SkillsPacket(client.Player.Skills));
            // set abilities
            client.CallMethod(client.Player.EntityId, new AbilitiesPacket(client.Player.Skills));   // ToDo
            // update allocation points
            SendAvailableAllocationPoints(client);
            // update database with new character skills
            foreach (var skill in skillLevelupArray)
            {
                var skillToUpdate = client.Player.Skills[skill.Value.SkillId];
                unitOfWork.CharacterSkills.AddOrUpdate(client.Player.Id, (uint)skillToUpdate.SkillId, skillToUpdate.AbilityId, skillToUpdate.SkillLevel);
            }
        }

        public void NotifyEquipmentUpdate(Client client)
        {
            client.CallMethod(client.Player.EntityId, new EquipmentInfoPacket(client.Player.Inventory.EquippedInventory));
        }

        /// <param name="delay">Milliseconds until the timer's first shot; a whole refire when left out.</param>
        /// <returns>false when there is no weapon in hand to fire, and no timer was started.</returns>
        public bool RegisterAutoFire(Client client, long delay = -1)
        {
            var weapon = InventoryManager.Instance.CurrentWeapon(client);

            if (weapon?.ItemTemplate?.WeaponInfo == null)
                return false;

            // One timer per client: a second StartAutoFire used to add a second timer and
            // double the rate of fire.
            RemoveAutoFire(client);

            var refire = weapon.ItemTemplate.WeaponInfo.Refire;

            AutoFire.Add(new AutoFireTimer(client, refire, delay < 0 ? refire : delay));

            return true;
        }

        private static void RemoveAutoFire(Client client)
        {
            for (var i = AutoFire.Count - 1; i >= 0; i--)
                if (AutoFire[i].Client == client)
                    AutoFire.RemoveAt(i);
        }

        public void RemovePlayerCharacter(Client client)
        {
            // Called from MapChannelManager.RemovePlayer. A client that dropped while holding
            // fire stayed in the auto-fire list; once its items were destroyed CurrentWeapon
            // was null, and the next tick dereferenced it on the main loop.
            RemoveAutoFire(client);
        }

        public void RemoveAppearanceItem(Client client, EquipmentData equipmentSlotId)
        {
            if (equipmentSlotId == 0)
                return;

            // A slot nothing was ever shown in is already clear. SetAppearanceItem is what adds
            // the key, so a slot whose item never got that far - one holding something with no
            // appearance at all - has no entry, and the indexer threw where there was simply
            // nothing to remove.
            if (!client.Player.AppearanceData.ContainsKey(equipmentSlotId))
                return;

            client.Player.AppearanceData[equipmentSlotId].Class = 0;
            // update appearance data in database
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterAppearances.AddOrUpdate(client.Player.Id, new CharacterAppearanceEntry((uint)equipmentSlotId, 0, 0));
            unitOfWork.Complete();
        }

        public void RequestCustomization(Client client, RequestCustomizationPacket packet)
        {
            // ToDo
            Logger.WriteLog(LogType.Debug, $"ToDo: RequestCustomization");
        }

        /// <summary>
        /// /stuck. Moves a player who has become wedged in the world to the nearest place they
        /// can stand.
        ///
        /// The position in the packet is the client's, and is not used: a client is free to claim
        /// it is stuck anywhere, and acting on that would turn this into a teleport. The server
        /// works from the position it holds, and the navmesh decides where they end up, so the
        /// destination is always somewhere they could have walked to.
        ///
        /// A dead player is left alone - they have a respawn for that, and moving a corpse would
        /// take the body away from anyone about to revive it.
        /// </summary>
        public void RequestUnstick(Client client, RequestUnstickPacket packet)
        {
            if (client.Player == null)
                return;

            if (client.Player.Attributes[Attributes.Health].Current <= 0 || client.Player.State == CharacterState.Dead)
            {
                Unhelpful(client, PlayerMessage.PmAboutToRespawn);
                return;
            }

            var from = client.Player.Position;
            var destination = NavMeshManager.NearestWalkable(client.Player.MapChannel, from);

            if (destination == null)
            {
                // No navmesh for this map, or the player is further from walkable ground than the
                // search reaches. Moving them somewhere arbitrary would be worse than saying so.
                Unhelpful(client, PlayerMessage.PmCannotPerformActionNow);
                Logger.WriteLog(LogType.Debug, $"Character {client.Player.Id} used /stuck at {from} on map {client.Player.MapChannel?.MapInfo?.MapContextId}, where nothing walkable was found.");
                return;
            }

            client.Player.Position = destination.Value;
            client.MoveObject(client.Player.EntityId, new Movement(destination.Value, client.Movement.ViewDirection));

            // The message the live game showed for this command.
            client.CallMethod(SysEntity.CommunicatorId,
                new DisplayClientMessagePacket(PlayerMessage.PmStuckBugReportSent, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));

            Logger.WriteLog(LogType.Debug, $"Character {client.Player.Id} unstuck from {from} to {destination.Value} ({Vector3.Distance(from, destination.Value):F1} m).");
        }

        private static void Unhelpful(Client client, PlayerMessage reason)
        {
            client.CallMethod(SysEntity.CommunicatorId,
                new DisplayClientMessagePacket(reason, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
        }

        /// <summary>
        /// Firing an armed ability. AbilityManager checks it - ownership, cooldown, target, range,
        /// cost - answers a refusal with UserActionFailed, and queues an accepted one for the
        /// windup; ActorActionManager hands the recovery back to it. The position of a
        /// ground-targeted ability travels in ActionData.TargetLocation.
        /// </summary>
        public void RequestPerformAbility(Client client, RequestPerformAbilityPacket packet)
        {
            // Ownership, cooldown, range, cost and the windup are AbilityManager's; this used to
            // queue the action for the next tick with no checks at all.
            AbilityManager.Instance.RequestPerformAbility(client, packet);
        }

        public void RequestToggleRun(Client client)
        {
            client.Player.IsRunning = !client.Player.IsRunning;

            client.CallMethod(client.Player.EntityId, new IsRunningPacket(client.Player.IsRunning));
        }

        /// <summary>Idle time before PlayerInactiveWarning, which also marks the player AFK.</summary>
        public const long InactiveWarningMs = 5 * 60 * 1000;

        /// <summary>Idle time before the player is sent back to character selection.</summary>
        public const long InactiveLogoutMs = 15 * 60 * 1000;

        /// <summary>
        /// Client traffic that does not mean the player is at the keyboard. It neither resets
        /// the inactivity timer nor clears AFK. Everything else the client sends counts as
        /// activity: movement, abilities, weapons, chat, inventory, targeting and so on.
        /// </summary>
        private static readonly HashSet<GameOpcode> AutomaticOpcodes = new()
        {
            GameOpcode.Ping,                // client resends every 1s from RequestNetworkStats()
            GameOpcode.AutoFireKeepAlive,   // client resends every 2.5s while autofire runs
            GameOpcode.MapLoaded,           // sent automatically once a zone finishes loading
            GameOpcode.TeleportAcknowledge  // automatic reply to a server-initiated teleport
        };

        /// <summary>
        /// Called for every inbound client method call. Resets the inactivity timer and clears
        /// AFK (telling everyone in range) the first time the player actually does something.
        /// </summary>
        public void NotifyPlayerActivity(Client client, GameOpcode opcode)
        {
            if (AutomaticOpcodes.Contains(opcode))
                return;

            ResetInactivity(client);

            // /afk is a deliberate action, so it resets the timer above, but clearing AFK here
            // would immediately undo the flag it is setting.
            if (opcode == GameOpcode.ToggleAfk)
                return;

            SetAfk(client, false);
        }

        /// <summary>Called for movement, which arrives outside CallServerMethod.</summary>
        public void NotifyPlayerActivity(Client client)
        {
            ResetInactivity(client);

            // SetAfk returns immediately when the flag is already clear, so this costs one
            // bool comparison on the movement path and only broadcasts on a real transition.
            SetAfk(client, false);
        }

        /// <summary>
        /// Starts a fresh idle stretch. Also called when a player enters the world or finishes
        /// a teleport, so time spent on a loading screen never counts as idle.
        /// </summary>
        public void ResetInactivity(Client client)
        {
            client.Player.LastActivityTick = Environment.TickCount64;
            client.Player.InactiveWarningSent = false;
        }

        /// <summary>
        /// Run from the MapChannelWorker every tick, ahead of its removal pass. At
        /// InactiveWarningMs the player is marked AFK and warned; at InactiveLogoutMs they are
        /// flagged the same way CharacterLogout flags a /logout, so the removal pass returns
        /// them to character selection on the same tick.
        /// </summary>
        public void CheckInactivity(MapChannel mapChannel)
        {
            var now = Environment.TickCount64;

            foreach (var client in mapChannel.ClientList)
            {
                // Loading, teleporting, already leaving, or a dropped connection awaiting removal.
                if (client == null || client.State != ClientState.Ingame || client.Player.RemoveFromMap || client.Player.Disconected)
                    continue;

                var idle = now - client.Player.LastActivityTick;

                if (idle >= InactiveLogoutMs)
                {
                    Logger.WriteLog(LogType.Network, $"{client.Player.FamilyName} inactive for {idle / 60000} minutes, returning to character selection");

                    // Same effect as MapChannelManager.CharacterLogout, without its LogoutActive
                    // gate: that flag only exists to confirm the client asked to leave.
                    // Recv_BeginCharacterSelection switches the client to character selection
                    // from any input state, so the client does not need to cooperate.
                    client.State = ClientState.LoggedIn;
                    client.Player.RemoveFromMap = true;
                    continue;
                }

                if (idle >= InactiveWarningMs && !client.Player.InactiveWarningSent)
                {
                    client.Player.InactiveWarningSent = true;

                    // AFK first, so the player reads "You are now AFK" followed by the warning,
                    // and everyone in range sees the idle marker.
                    SetAfk(client, true);
                    client.CallMethod(client.Player.EntityId, new PlayerInactiveWarningPacket());
                }
            }
        }

        public void ToggleAfk(Client client)
        {
            SetAfk(client, !client.Player.IsAFK);
        }

        public void SetAfk(Client client, bool isAfk)
        {
            if (client.Player.IsAFK == isAfk)
                return;

            client.Player.IsAFK = isAfk;

            // Broadcast to everyone in visibility range, including the player: the client
            // shows the "you are AFK" system message only for its own manifestation, and an
            // idle indicator over anyone else's head.
            client.CellCallMethod(client, client.Player.EntityId, new PlayerAfkPacket(isAfk));

            // The squad window reads a different source: its own party tuples, not the
            // manifestation. A squadmate on another map is not in visibility range at all, so
            // without this they never learn the member went away.
            PartyManager.Instance.MemberInfoChanged(client);
        }

        /// <summary>
        /// Also a handler: the client asks to draw, and can ask with an empty weapon drawer
        /// slot armed. There is then no weapon to name a draw animation, so there is nothing
        /// to perform and nothing to be ready with.
        /// </summary>
        public void RequestWeaponDraw(Client client)
        {
            var mapChannel = client.Player?.MapChannel;

            if (mapChannel == null)
                return;

            var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(InventoryManager.Instance.CurrentWeapon(client));

            if (weaponClassInfo == null)
                return;

            QueueWeaponReadyChange(mapChannel, new ActionData(client.Player, ActionId.WeaponDraw, weaponClassInfo.DrawActionId, 500));

            WeaponReady(client, true);
        }

        /// <summary>
        /// Puts a draw or a stow in the queue as the only one this actor has.
        ///
        /// Both handlers used to add one per packet with nothing to stop them. The client sends
        /// one per keypress, so a client sending them in a loop grew the map's recovery list
        /// without bound - a list walked on every tick of the map's worker, each entry coming
        /// due with its own PerformRecovery to everyone in range. Only the last one asked for
        /// means anything anyway: a stow that arrives while a draw is still playing replaces it
        /// rather than lining up behind it, which is also what the player meant by sending it.
        /// </summary>
        private static void QueueWeaponReadyChange(MapChannel mapChannel, ActionData action)
        {
            mapChannel.PerformRecovery.RemoveAll(queued => queued.Actor == action.Actor
                                                           && (queued.ActionId == ActionId.WeaponDraw || queued.ActionId == ActionId.WeaponStow));

            mapChannel.PerformRecovery.Add(action);
        }

        /// <summary>
        /// Ends a reload the server is not going to finish.
        ///
        /// The client plays the reload animation as a windup and waits to be told how it ended:
        /// the recovery resolves it, or an interrupt cancels it. Returning without either leaves
        /// the animation running until some other action happens to interrupt it, which is what
        /// an out-of-ammo reload did - it failed correctly and then span forever.
        ///
        /// Recv_ActionInterrupt matches the action and its arg against the actor's current
        /// action, so the arg has to be the one the windup was started with. It goes to everyone
        /// in range, not just the player: onlookers were shown the windup too.
        /// </summary>
        private void CancelReload(Client client, uint reloadActionId, PlayerMessage reason)
        {
            client.CellCallMethod(client, client.Player.EntityId,
                new ActionInterruptPacket(client.Player.EntityId, ActionId.WeaponReload, reloadActionId));

            client.CallMethod(SysEntity.CommunicatorId,
                new DisplayClientMessagePacket(reason, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
        }

        public void RequestWeaponReload(Client client, bool isRequested)
        {
            // One reload at a time. Every request used to queue another, with a windup to everyone
            // in range for each: the client never sends a second while its first is in progress,
            // but the fire path asks again on every trigger pull that finds the clip empty, so
            // holding fire through a reload queued another every refire, and a client sending the
            // request in a loop queued one per packet - each coming due with its own recovery to
            // everyone in range and its own database write.
            if (client.Player == null || IsReloading(client.Player))
                return;

            // here we only check, can we reload weapon
            // actual weapon reload happen if reaload action isn't interupted
            var weapon = InventoryManager.Instance.CurrentWeapon(client);

            if (weapon?.ItemTemplate?.WeaponInfo == null)
                return;

            var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(weapon);

            if (weaponClassInfo == null)
                return;

            var reloadActionId = (uint)weaponClassInfo.ReloadActionId;
            var foundAmmo = 0u;

            for (var i = 0; i < 50; i++)
            {
                if (client.Player.Inventory.PersonalInventory[(int)InventoryOffset.CategoryConsumable + i] == 0)
                    continue;

                var weaponAmmo = EntityManager.Instance.GetItem(client.Player.Inventory.PersonalInventory[(int)InventoryOffset.CategoryConsumable + i]);

                // A slot naming an item that is not registered. Skip that slot rather than
                // abandoning the reload: one stale row used to stop the scan, so ammo sitting in
                // a later slot was never found and the reload silently did nothing.
                if (weaponAmmo == null)
                    continue;

                if (weaponAmmo.ItemTemplate.Class == weaponClassInfo.AmmoClassId)
                {
                    // consume ammo
                    var ammoToGrab = Math.Min(weaponClassInfo.ClipSize - foundAmmo - weapon.CurrentAmmo, weaponAmmo.StackSize);
                    foundAmmo = ammoToGrab + weapon.CurrentAmmo;
                }

                if (foundAmmo == weaponClassInfo.ClipSize)
                    break;
            }

            // A jammed weapon reloads regardless of what is in the clip or the pack, because
            // reloading is the only way to clear a jam and the client already works this way:
            // weaponreload.py runs its ammo, clip-full and out-of-ammo checks inside
            // `if not weapon.isJammed`. Without this, a player who jams with a full clip or an
            // empty pack has no way out of it.
            if (weapon.IsJammed)
            {
                if (isRequested)
                    client.CellCallMethod(client, client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, ActionId.WeaponReload, reloadActionId));
                else
                    client.CellIgnoreSelfCallMethod(client, new PerformWindupPacket(PerformType.TwoArgs, ActionId.WeaponReload, reloadActionId));

                client.Player.MapChannel.PerformRecovery.Add(new ActionData(client.Player, ActionId.WeaponReload, reloadActionId, foundAmmo, weapon.ItemTemplate.WeaponInfo.ReloadTime));
                return;
            }

            if (foundAmmo == 0)
            {
                // Nothing to reload with.
                //
                // isRequested is false when the *player* asked: their own client has already
                // started the animation locally, which is why the windup below is sent to
                // everyone except them. So that is exactly the case where the client is sitting
                // in a windup nothing will ever end, and it has to be told.
                //
                // isRequested is true only for the reload the fire path starts when the clip is
                // empty. No windup has been sent yet, so there is nothing to cancel - and firing
                // a dry weapon comes back here on every trigger pull, so saying anything would
                // be a message per tick.
                if (!isRequested)
                    CancelReload(client, reloadActionId, PlayerMessage.PmInventoryOutOfAmmo);

                return;
            }

            if (isRequested)
                client.CellCallMethod(client, client.Player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, ActionId.WeaponReload, (uint)weaponClassInfo.ReloadActionId));
            else
                client.CellIgnoreSelfCallMethod(client, new PerformWindupPacket(PerformType.TwoArgs, ActionId.WeaponReload, (uint)weaponClassInfo.ReloadActionId));

            client.Player.MapChannel.PerformRecovery.Add(new ActionData(client.Player, ActionId.WeaponReload, (uint)weaponClassInfo.ReloadActionId, foundAmmo, weapon.ItemTemplate.WeaponInfo.ReloadTime));
        }

        /// <summary>
        /// Also a handler, and reachable with an empty weapon drawer slot armed, same as
        /// RequestWeaponDraw. An empty hand is already stowed: there is no animation to
        /// perform, but the flag is still cleared, because that is the truth either way.
        /// </summary>
        public void RequestWeaponStow(Client client)
        {
            var mapChannel = client.Player?.MapChannel;

            if (mapChannel == null)
                return;

            var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(InventoryManager.Instance.CurrentWeapon(client));

            if (weaponClassInfo != null)
                QueueWeaponReadyChange(mapChannel, new ActionData(client.Player, ActionId.WeaponStow, (uint)weaponClassInfo.StowActionId, 500));

            WeaponReady(client, false);
        }

        public void SaveCharacterOptions(Client client, SaveCharacterOptionsPacket packet)
        {
            if (packet.OptionsList.Count == 0)
                return;

            client.Player.CharacterOptions = packet.OptionsList;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            foreach (var option in client.Player.CharacterOptions)
                unitOfWork.CharacterOptions.AddOrUpdate(client.Player.Id, (uint)option.OptionId, option.Value);

            // AddOrUpdate only stages the rows; without this they were thrown away on dispose,
            // and every option the client saved was back to its default at the next login.
            unitOfWork.Complete();
        }

        // maybe move this to other manager becose it's account related
        public void SaveUserOptions(Client client, SaveUserOptionsPacket packet)
        {
            client.UserOptions = packet.OptionsList;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            foreach (var option in client.UserOptions)
                unitOfWork.UserOptions.AddOrUpdate(client.AccountEntry.Id, (uint)option.OptionId, option.Value);

            unitOfWork.Complete();
        }

        internal void SendAvailableAllocationPoints(Client client)
        {
            // update available allocation points (attributes, trainPts, skillPts)

            var attributePoints = GetAvailableAttributePoints(client.Player);
            var trainPoints = 0;    // not used by te client
            var skillPoints = GetSkillPointsAvailable(client.Player);

            client.CallMethod(client.Player.EntityId, new AvailableAllocationPointsPacket(attributePoints, trainPoints, skillPoints));
        }

        public void SetAppearanceItem(Client client, Item item)
        {
            var equipable = EntityClassManager.Instance.GetEquipableClassInfo(item);

            // Only equipment is worn. Every caller establishes that before asking, so reaching
            // here without it is a caller that stopped checking rather than something a player
            // did - said plainly instead of thrown, which used to take the connection down.
            if (equipable == null)
            {
                Logger.WriteLog(LogType.Error,
                    $"SetAppearanceItem was given {item?.ItemTemplate?.Class.ToString() ?? "no item"}, which has no equipment slot; appearance unchanged.");
                return;
            }

            var equipmentSlotId = equipable.EquipmentSlotId;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (!client.Player.AppearanceData.ContainsKey(equipmentSlotId))
                client.Player.AppearanceData.Add(equipmentSlotId, new AppearanceData { SlotId = equipmentSlotId });

            client.Player.AppearanceData[equipmentSlotId].Class = (uint)item.ItemTemplate.Class;
            client.Player.AppearanceData[equipmentSlotId].Color = new Color(item.Color);
            client.Player.AppearanceData[equipmentSlotId].Hue2 = new Color(item.Color);

            // update appearance data in database

            unitOfWork.CharacterAppearances.AddOrUpdate(client.Player.Id, new CharacterAppearanceEntry((uint)equipmentSlotId, (uint)item.ItemTemplate.Class, item.Color));
            unitOfWork.Complete();
        }

        public void SetDesiredCrouchState(Client client, bool crouching)
        {
            client.Player.IsCrouching = crouching;

            client.CallMethod(client.Player.EntityId, new SetDesiredCrouchStatePacket(client.Player.IsCrouching ? CharacterState.Crouched : CharacterState.Standing));
        }

        public void SetTargetId(Client client, ulong entityId)
        {
            client.Player.Target = entityId;
        }

        public void SetTrackingTarget(Client client, ulong entityId)
        {
            client.Player.TrackingTargetEntityId = entityId;
        }

        public void UpdateAppearance(Client client)
        {
            if (client.Player == null)
                return;

            client.CellCallMethod(client, client.Player.EntityId, new AppearanceDataPacket(client.Player.AppearanceData));
        }

        // Health calculation:
        //levelBasedHealth = 20000.0
        //for (int i = level; i< 50; ++i)
        //levelBasedHealth = levelBasedHealth - 0.082995 * levelBasedHealth;

        private readonly float[] HealthBaselinePerLevel =
         {
            286.5784148f,
            312.51565127f,
            340.8003787f,
            371.6450605f,
            405.28138942f,
            441.96202792f,
            481.96250612f,
            525.58329139f,
            573.15204539f,
            625.02608535f,
            681.59506802f,
            743.28391668f,
            810.55601298f,
            883.91667764f,
            963.91696626f,
            1051.15780858f,
            1146.29452247f,
            1250.04173638f,
            1363.17875735f,
            1486.55542483f,
            1621.09849437f,
            1767.818599f,
            1927.81784069f,
            2102.29806892f,
            2292.56990847f,
            2500.06260431f,
            2726.33475751f,
            2973.08603281f,
            3242.1699258f,
            3535.60768567f,
            3855.60349799f,
            4204.56104164f,
            4585.10154431f,
            5000.08347207f,
            5452.62400104f,
            5946.1224323f,
            6484.28572615f,
            7071.15634718f,
            7711.14262974f,
            8409.05189147f,
            9170.12654399f,
            10000.08347172f,
            10905.15697485f,
            11892.14559882f,
            12968.4632023f,
            14142.19464703f,
            15422.15652808f,
            16817.9634005f,
            18340.1f,
            20000f
        };

        /*
         * ToDO (this still need work, this is just copied from c++ projet
         * Updates all attributes depending on level, spent attribute points, etc.
         * Does not send values to clients
         * If fullreset is true, the current values of each attribute are set to the maximum
         */
        public void UpdateStatsValues(Client client, bool fullreset)
        {
            var player = client.Player;
            var attribute = player.Attributes;

            int level = player.Level;

            // We don't want things to blow up just in case something wrong happens to level
            if (level < 0) level  = 1;
            if (level > 50) level = 50;

            int levelBasedBody   = 0;
            int levelBasedMind   = 0;
            int levelBasedSpirit = 0;

            switch (player.Race)
            {
                case Race.Human:
                    levelBasedBody = levelBasedMind = levelBasedSpirit = 2 * (level - 1) + 10;
                    break;

                case Race.Forean:
                    levelBasedBody = (level - 1) + 10;
                    levelBasedMind = 3 * (level - 1) + 10;
                    levelBasedSpirit = 2 * (level - 1) + 10;
                    break;

                case Race.Brann:
                    levelBasedBody = levelBasedMind = (level - 1) + 10;
                    levelBasedSpirit = 4 * (level - 1) + 10;
                    break;

                case Race.Thrax:
                    levelBasedBody = 3 * (level - 1) + 10;
                    levelBasedMind = (level - 1) + 10;
                    levelBasedSpirit = 2 * (level - 1) + 10;
                    break;
            }

            int totalBody   = levelBasedBody   + player.SpentBody;
            int totalMind   = levelBasedMind   + player.SpentMind;
            int totalSpirit = levelBasedSpirit + player.SpentSpirit;

            // Health
            float levelBasedHealth = HealthBaselinePerLevel[level - 1];
            levelBasedHealth = levelBasedHealth / (2 * (level - 1) + 2 * (2 * (level - 1) + 10) + 10);
            int totalHealth = (int)(levelBasedHealth * (totalSpirit + 2 * totalBody));

            // The per-point factor shrinks with level; the attribute totals grow. Both sides of
            // these divisions were int, so the factor was truncated: 3 at level 1 instead of
            // 3.43, 1 from level 20 (1.11), and 0 from level 26 for power and 39 for regen - a
            // character that levelled far enough had no chi and no regeneration at all. The
            // health line above already divides as float and was right.
            float attributeDivisor = 2 * (level - 1) + 2 * (2 * (level - 1) + 10) + 10;

            // Power
            float basePower = (3 * level + 100) / attributeDivisor;
            int totalPower  = (int)(basePower * (totalBody + 2 * totalMind));

            // Regen
            float baseRegen = (2 * level + 100) / attributeDivisor;
            int totalRegen = (int)(baseRegen * (totalMind + 2 * totalSpirit));
          
            // Bonuses
            var bodyBonus = 0;
            var mindBonus = 0;
            var spiritBonus = 0;

            var healthBonus = 0;
            var chiBonus    = 0;
            var regenBonus  = 0;

            float armorBonusPercent = (float)Math.Max(0.0, (totalBody - (2 * (level - 1) + 10)) * 0.667);   // every body attribute over the default base attribute gives 0.667% bonus armo;
            float logosBonusPercent = (float)Math.Max(0.0, (totalMind - (2 * (level - 1) + 10)) * 0.375);   // every mind attribute over the default base attribute gives 0.375% bonus logos damage
            float critBonusPercent = (float)Math.Max(0.0, (totalSpirit - (2 * (level - 1) + 10)) * 0.065);  // every spirit attribute over the default base attribute gives 0.065% bonus crit chance;


            // body
            attribute[Attributes.Body].NormalMax    = totalBody;
            attribute[Attributes.Body].CurrentMax   = attribute[Attributes.Body].NormalMax + bodyBonus;
            attribute[Attributes.Body].Current      = attribute[Attributes.Body].Current;

            attribute[Attributes.Mind].NormalMax    = totalMind;
            attribute[Attributes.Mind].CurrentMax   = attribute[Attributes.Mind].NormalMax + mindBonus;
            attribute[Attributes.Mind].Current      = attribute[Attributes.Mind].Current;

            attribute[Attributes.Spirit].NormalMax  = totalSpirit;
            attribute[Attributes.Spirit].CurrentMax = attribute[Attributes.Spirit].NormalMax + spiritBonus;
            attribute[Attributes.Spirit].Current    = attribute[Attributes.Spirit].CurrentMax;

            // health
            attribute[Attributes.Health].NormalMax  = totalHealth;
            attribute[Attributes.Health].CurrentMax = totalHealth;

            // chi/adrenaline
            attribute[Attributes.Chi].NormalMax     = totalPower;
            attribute[Attributes.Chi].CurrentMax    = totalPower;

            attribute[Attributes.Regen].NormalMax   = totalRegen; // regenRate in percent
            attribute[Attributes.Regen].CurrentMax  = totalRegen;

            if (fullreset)
            {
                attribute[Attributes.Health].Current = attribute[Attributes.Health].CurrentMax;
                attribute[Attributes.Chi].Current = attribute[Attributes.Chi].CurrentMax;
            }
            else
            {
                attribute[Attributes.Health].Current = Math.Min(attribute[Attributes.Health].Current, attribute[Attributes.Health].CurrentMax);
                attribute[Attributes.Chi].Current = Math.Min(attribute[Attributes.Chi].Current, attribute[Attributes.Chi].CurrentMax);
            }


            // update regen rate: 2.0 per second at 100% regen, scaled by the rate as a
            // percentage. CurrentMax / 100 was int division, so any rate below 200% rounded
            // to the base 2 and the rate only mattered in whole multiples of 100.
            //
            // It goes on Health, not on Regen. Regen is a derived stat the attributes window
            // displays; nothing regenerates from it. The client heals from Health's own
            // refreshAmount and refreshPeriod, and Manifestation initialises both to 0, so
            // computing the rate and storing it on the wrong attribute meant no player has ever
            // regenerated health at all. The period has to be non-zero as well:
            // _EvaluatePredictedRefresh returns early on a period of 0.
            attribute[Attributes.Health].RefreshAmount = (int)Math.Round(2D * attribute[Attributes.Regen].CurrentMax / 100, 0);

            // Power regenerates at the health rate for now. The live game regenerated power by a
            // formula of its own that is not known; without any regeneration an ability could be
            // used a handful of times per map, since abilities now spend it. Interim.
            // ActorManager.Regenerate applies it server-side at the same period the client
            // predicts it with.
            //
            // Chi (adrenaline) does not regenerate. The client's own text has it "consumed by
            // specific abilities such as Rage and Sprint" and "gained by defeating enemies or by
            // using an adrenaline booster", with the Regen stat improving "Adrenaline gain" -
            // it is earned in combat, not refilled over time, and a passive refill would outpace
            // sprint's 1.5% a second drain and make it free. Kills grant it in
            // CreatureManager.HandleCreatureKill; its RefreshAmount stays 0 so the client
            // predicts nothing.
            attribute[Attributes.Power].RefreshAmount = attribute[Attributes.Health].RefreshAmount;
            attribute[Attributes.Chi].RefreshAmount = 0;
            // 2.0 per second is the base regeneration for health
            // calculate armor max
            var armorMax = 0.0d;
            //float armorBonus = 0; // todo! (From item modules)
            var armorBonusPct = player.Attributes[Attributes.Body].CurrentMax * 0.0066666d;
            var armorRegenRate = 0;

            for (var i = 1; i < 21; i++)
            {
                if (client.Player.Inventory.EquippedInventory[i] == 0)
                    continue;

                // skip weapon slot
                if (i == 13)
                    continue;

                var equipmentItem = EntityManager.Instance.GetItem(client.Player.Inventory.EquippedInventory[i]);
                var classInfo = EntityClassManager.Instance.GetClassInfo(equipmentItem.ItemTemplate.Class);

                if (equipmentItem == null)
                {
                    // this is very bad, how can the item disappear while it is still linked in the inventory?
                    Logger.WriteLog(LogType.Error, "UpdateStatsValues: Equipment item has no physical copy (item is missing)");
                    continue;
                }
                if (classInfo.ArmorClassInfo == null)
                {
                    // how can the player equip non-armor?
                    Logger.WriteLog(LogType.Error, "UpdateStatsValues: Player try to equip non_armor item");
                    continue;
                }
                armorMax += equipmentItem.ItemTemplate.ArmorValue;      // ToDo
                armorRegenRate += classInfo.ArmorClassInfo.RegenRate;
                
                // what about damage absorbed? Was it used at all?
            }
            armorMax = armorMax * (1.0d + armorBonusPct);

            // The regen rate summed off the equipped armour goes on RefreshAmount. It used to be
            // assigned to Current, which the fullreset branch a few lines below overwrites
            // unconditionally - so it was computed, discarded, and armour never regenerated
            // either.
            attribute[Attributes.Armor].RefreshAmount = armorRegenRate;
            attribute[Attributes.Armor].NormalMax = (int)Math.Round(armorMax, 0);
            attribute[Attributes.Armor].CurrentMax = attribute[Attributes.Armor].NormalMax;
            if (fullreset)
                attribute[Attributes.Armor].Current = attribute[Attributes.Armor].CurrentMax;
            else
                attribute[Attributes.Armor].Current = Math.Min(attribute[Attributes.Armor].Current, attribute[Attributes.Armor].CurrentMax);
            // added by krssrb
            // power test
            attribute[Attributes.Power].NormalMax = 100 + (player.Level - 1) * 2 * 4 + player.SpentMind * 3;
            var powerBonus = 0;
            attribute[Attributes.Power].CurrentMax = attribute[Attributes.Power].NormalMax + powerBonus;
            if (fullreset)
                attribute[Attributes.Power].Current = attribute[Attributes.Power].CurrentMax;
            else
                attribute[Attributes.Power].Current = Math.Min(attribute[Attributes.Power].Current, attribute[Attributes.Power].CurrentMax);

            // The rates above are the out-of-combat ones. A player recomputing their stats while
            // in a fight - equipping something, levelling - keeps the penalty.
            ApplyRegenPeriod(player);
        }

        public void WeaponReady(Client client, bool isReady)
        {
            client.Player.WeaponReady = isReady;
            client.CallMethod(client.Player.EntityId, new WeaponReadyPacket(isReady));
        }

        public void WeaponReload(ActionData action)
        {
            // we reload weapon here
            var client = Server.Clients.Find(c => c.Player == action.Actor);

            // The reload was queued with a delay, and the player can be gone by the time it
            // fires - the connection dropped, the character logged out or was summoned away.
            // RemovePlayer now clears their queued actions, but this runs on the world loop,
            // where a null here used to end the process, so it is checked as well.
            if (client == null || client.State != ClientState.Ingame)
                return;

            // Interrupted before it finished. WeaponReload sets actionInterrupts, so the client
            // interrupts its own reload when the player performs another action - a melee or an
            // alternate attack; primary fire waits for the reload instead - cancelling it on its
            // side as it sends RequestActionInterrupt. ActorActionManager brings an
            // interrupted action forward to be seen to at once, and this took that for the reload
            // finishing: the clip was filled and the jam cleared on the next tick. Any melee or
            // alternate attack mid-reload was an instant reload, and a request followed at once by
            // an interrupt cleared a jam in one tick, so the jam never cost anything.
            //
            // It loads nothing, and the jam stays. Everyone in range was shown the windup and is
            // told it ended; the player's own client already cancelled it.
            if (action.IsInrerrupted)
            {
                client.CellIgnoreSelfCallMethod(client, new ActionInterruptPacket(client.Player.EntityId, ActionId.WeaponReload, action.ActionArgId));
                return;
            }

            var weapon = InventoryManager.Instance.CurrentWeapon(client);

            if (weapon == null)
                return;

            var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(weapon);

            if (weaponClassInfo == null)
                return;

            // The reload finished, so the jam is cleared - whether or not a single round went in.
            // A jam with a full clip still takes a reload to clear, and that reload loads nothing.
            ClearJam(client, weapon);

            // What is in the clip now, topped up stack by stack until it is full. The old
            // arithmetic subtracted CurrentAmmo again on every stack after the first, and in
            // uint that wrapped, so the second stack was taken whole. It never showed because
            // ReduceStackCount did not actually consume anything until now.
            var loaded = Math.Min(weapon.CurrentAmmo, weaponClassInfo.ClipSize);

            for (var i = 0; i < 50 && loaded < weaponClassInfo.ClipSize; i++)
            {
                var entityId = client.Player.Inventory.PersonalInventory[(int)InventoryOffset.CategoryConsumable + i];

                if (entityId == 0)
                    continue;

                var weaponAmmo = EntityManager.Instance.GetItem(entityId);

                if (weaponAmmo == null || weaponAmmo.ItemTemplate.Class != weaponClassInfo.AmmoClassId || weaponAmmo.StackSize == 0)
                    continue;

                var ammoToGrab = Math.Min(weaponClassInfo.ClipSize - loaded, weaponAmmo.StackSize);

                loaded += ammoToGrab;
                InventoryManager.Instance.ReduceStackCount(client, InventoryType.Personal, weaponAmmo, ammoToGrab);
            }

            // update the ammo count
            weapon.CurrentAmmo = loaded;

            // update db
            ItemManager.Instance.UpdateItemCurrentAmmo(weapon);

            // set current action to 0
            client.Player.CurrentAction = 0;

            // send data to client
            client.CellCallMethod(client, client.Player.EntityId, new PerformRecoveryPacket(PerformType.ThreeArgs, action.ActionId, action.ActionArgId, loaded));
        }

        #endregion
    }
}
