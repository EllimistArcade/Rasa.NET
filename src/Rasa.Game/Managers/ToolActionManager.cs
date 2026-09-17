using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;

    /// <summary>
    /// Equipped tools: the healing disc, the field repair tool, armour augmentation, the cipher,
    /// the harvesting tools, and the snowball launcher.
    ///
    /// These are weapons as far as the inventory is concerned - armed in the weapon slot, drawn,
    /// reloaded, jammed - but firing one sends RequestToolAction rather than RequestWeaponAttack.
    /// The client makes that choice by itself from the armed item's entity class, so the server
    /// cannot steer a tool down the weapon path: it either answers this opcode or the player is
    /// disconnected by the terminator check on an opcode with no handler. 719 of the item
    /// templates we seed are tools, and two of them - the level 5-9 field repair tool and area
    /// healing disc - are on service-NPC vendors, so this is reachable by a new character.
    ///
    /// Three of the six do something: the healing disc, the field repair tool and armour
    /// augmentation. Harvesting and the cipher are validated and then refused silently, because
    /// asking for them is not a mistake - one needs a loot path and the other the usable-hack
    /// flow, and neither exists yet.
    ///
    /// Validation runs whatever happens, and it is the half that has to be right either way: the
    /// client's own CheckAction is a courtesy to the player, not a constraint on the wire.
    ///
    /// A use runs in two halves, as everything with a windup does. The request settles the
    /// amount and queues an ActionData on the map channel; ActorActionManager fires
    /// <see cref="PerformRecovery"/> when the windup has run, and that is where anything changes.
    /// Ammo and heat are spent at the request, like a shot, so a windup that gets interrupted
    /// still costs what it costs.
    ///
    /// The amounts are recovered rather than invented. Each tool's entity class carries min and
    /// max damage, equal on every class of all three, and that number is what the client's own
    /// tooltip prints next to "Healing:" or "Repair:" - _AddWeaponToolInfo is handed maxDamage as
    /// its maxAmt. A level 5-9 healing disc heals 127, its field repair counterpart repairs 190,
    /// and both scale to five figures at the cap.
    ///
    /// Three things the client does that the server cannot match yet, marked ToDo rather than
    /// guessed at: creature flags (BIOLOGICAL / MECHANICAL / MACHINA) are not loaded server-side,
    /// there is no cipherable flag on dynamic objects, and the cone and radial variants are
    /// treated as single-target because ae_radius is a placeholder 1 on all 2440
    /// itemtemplate_weapon rows. All three refuse or narrow conservatively.
    /// </summary>
    public class ToolActionManager
    {
        private static ToolActionManager _instance;
        private static readonly object InstanceLock = new object();

        /// <summary>
        /// The six action ids whose client module lives under <c>client/actions/tools/</c>, which
        /// is exactly the set that can arrive on this opcode. Read out of the client's own
        /// <c>actiondata.actionModules</c> table; <c>tools/bufftool.py</c> exists but no action id
        /// maps to it, so it is not here.
        /// </summary>
        public static readonly HashSet<ActionId> ToolActions = new HashSet<ActionId>
        {
            ActionId.ToolHealingDisc,           // 147, tools.healdisc
            ActionId.ToolHarvest,               // 172, tools.harvest
            ActionId.ToolFieldRepair,           // 198, tools.repairtool
            ActionId.ToolArmorAugmentation,     // 199, tools.armoraug
            ActionId.ToolCipher,                // 258, tools.cipher
            ActionId.ToolNerfweapon             // 527, tools.nerfweapon
        };

        /// <summary>
        /// The three that do something. Harvesting needs a loot path and the cipher needs the
        /// usable-hack flow, so both are still refused - but silently, since there is nothing
        /// wrong with asking.
        /// </summary>
        public static readonly HashSet<ActionId> Applies = new HashSet<ActionId>
        {
            ActionId.ToolHealingDisc,
            ActionId.ToolFieldRepair,
            ActionId.ToolArmorAugmentation
        };

        /// <summary>
        /// The healing skill level at which healdisc.py sets <c>canTargetDead</c>, letting the
        /// healing disc and field repair tool be aimed at a corpse.
        /// </summary>
        public const int CorpseTargetHealingSkill = 3;

        /// <summary>
        /// The self variants, from the client's actiondata table:
        /// TOOL_USE_HEALING_DISC_SELF = 6 and TOOL_USE_FIELD_REPAIR_SELF = 4. Neither appears on
        /// any weapon class we have - the classes are all direct (1), cone (healing disc 4, field
        /// repair 2) and radial (5 and 3) - but the client will send them if a class ever does.
        /// </summary>
        public const uint HealingDiscSelf = 6;
        public const uint FieldRepairSelf = 4;

        /// <summary>
        /// Used when the tool has no itemtemplate_weapon row to give a windup. 519 of the 719
        /// tool templates have none; the 160 healing disc and field repair rows that do all read
        /// 800.
        /// </summary>
        public const long DefaultWindupMs = 800;

        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public static ToolActionManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new ToolActionManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private ToolActionManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        public void RequestToolAction(Client client, RequestToolActionPacket packet)
        {
            if (client?.Player == null || client.State != ClientState.Ingame)
                return;

            var refusal = Validate(client, packet);

            if (refusal != null)
            {
                Fail(client, packet, refusal);
                return;
            }

            // Harvesting and the cipher still do nothing, so they are refused silently - with
            // msgId None. The client cancels the action and pops it off __unresolvedActions
            // either way; what it skips is the message, which is deliberate: the player did
            // nothing wrong, and telling them off on every click of a tool that is simply not
            // finished says less than nothing.
            if (!Applies.Contains(packet.ActionId))
            {
                Fail(client, packet, null);
                return;
            }

            Begin(client, packet);
        }

        /// <summary>
        /// Starts the windup. The amount is settled here, from the tool that is armed now, and
        /// carried on the queued action - so swapping tools mid-windup cannot change what lands,
        /// and <see cref="PerformRecovery"/> checks the tool is still the same one anyway.
        /// </summary>
        private void Begin(Client client, RequestToolActionPacket packet)
        {
            var mapChannel = client.Player.MapChannel;

            if (mapChannel == null)
            {
                Fail(client, packet, null);
                return;
            }

            var tool = InventoryManager.Instance.CurrentWeapon(client);
            var classInfo = ClassInfoOf(tool);

            // Validate established both of these; re-read rather than pass them down so there is
            // one way to get at them.
            if (classInfo == null)
            {
                Fail(client, packet, null);
                return;
            }

            // Every one of these tools has min damage equal to max on every class it has, so the
            // amount is flat rather than a roll - and it is the number the client's own tooltip
            // shows, since _AddWeaponToolInfo is handed maxDamage as its maxAmt.
            var amount = classInfo.MaxDamage;

            SpendShot(client, tool);

            var targetId = TargetOf(client, packet);

            // The actor's own client is already showing the windup it started; the others need
            // telling. The recovery goes to everyone, including the caster, because the amount is
            // only known here.
            SendToOthers(mapChannel, client.Player,
                new PerformWindupPacket(PerformType.ThreeArgs, packet.ActionId, packet.ActionArgId, targetId));

            var windup = tool.ItemTemplate.WeaponInfo?.Windup ?? DefaultWindupMs;

            mapChannel.PerformRecovery.Add(
                new ActionData(client.Player, packet.ActionId, packet.ActionArgId, targetId, windup)
                {
                    Args = (uint)Math.Max(amount, 0)
                });
        }

        /// <summary>
        /// Who the tool is being used on. A self variant is always the caster whatever the client
        /// named, and a request that named nobody falls back to the caster: all three of these
        /// tools are TARGET_FRIENDLY or TARGET_SELF, so there is no other sensible recipient, and
        /// the client only leaves the target out when it had none.
        /// </summary>
        private static ulong TargetOf(Client client, RequestToolActionPacket packet)
        {
            if (IsSelfVariant(packet.ActionId, packet.ActionArgId))
                return client.Player.EntityId;

            return packet.Target.HasEntity ? packet.Target.EntityId : client.Player.EntityId;
        }

        /// <summary>
        /// The arg ids from the client's actiondata table. Cone and radial are area variants, and
        /// they are treated as single-target here: ae_radius is a placeholder 1 on all 2440
        /// itemtemplate_weapon rows, so there is no honest radius to gather targets inside. With
        /// ae_type sent as None the client stops nulling their targets, so a radial disc aims at
        /// whoever the player has selected, which is the closest thing to right that the data
        /// supports.
        /// </summary>
        private static bool IsSelfVariant(ActionId actionId, uint argId)
        {
            return (actionId == ActionId.ToolHealingDisc && argId == HealingDiscSelf)
                   || (actionId == ActionId.ToolFieldRepair && argId == FieldRepairSelf);
        }

        /// <summary>Ammo and heat, exactly as firing a weapon spends them.</summary>
        private void SpendShot(Client client, Item tool)
        {
            var perShot = tool.ItemTemplate.WeaponInfo?.AmmoPerShot ?? 0;

            if (perShot > 0 && tool.CurrentAmmo >= perShot)
            {
                tool.CurrentAmmo -= perShot;
                client.CallMethod(tool.EntityId, new WeaponAmmoInfoPacket(tool.CurrentAmmo));

                using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
                unitOfWork.Items.UpdateAmmo(tool);
            }

            // A tool barrel heats like any other. Done after the shot is paid for, so a shot that
            // did not happen heats nothing.
            ManifestationManager.Instance.AddWeaponHeat(client, tool);
        }

        /// <summary>
        /// Called from ActorActionManager once the windup has run. What actually happens to the
        /// target, and the only place it does.
        /// </summary>
        public void PerformRecovery(MapChannel mapChannel, ActionData action)
        {
            var client = Server.Clients.Find(c => c.Player == action.Actor);

            // The windup was queued with a delay and the player can be gone by the time it fires.
            if (client?.Player == null || client.State != ClientState.Ingame)
                return;

            if (action.IsInrerrupted)
            {
                SendToOthers(mapChannel, action.Actor,
                    new ActionInterruptPacket(action.Actor.EntityId, action.ActionId, action.ActionArgId));
                return;
            }

            // Still holding the tool that started this? Stowing or swapping mid-windup means
            // nothing lands, which is why the check is here and not only at the request.
            var classInfo = ClassInfoOf(InventoryManager.Instance.CurrentWeapon(client));

            if (classInfo == null || classInfo.WeaponAttackActionId != action.ActionId
                                  || classInfo.WeaponAttackArgId != action.ActionArgId)
                return;

            var target = ResolveTarget(action.TargetId);

            if (target == null)
                return;

            var amount = (int)action.Args;
            var hits = new List<ToolHit>();

            switch (action.ActionId)
            {
                case ActionId.ToolHealingDisc:
                {
                    var healed = ActorManager.Instance.Heal(target, amount, action.Actor.EntityId);

                    if (healed > 0)
                        hits.Add(new ToolHit(target.EntityId, healed));

                    break;
                }

                case ActionId.ToolArmorAugmentation:
                {
                    var armored = ActorManager.Instance.RestoreArmor(target, amount, action.Actor.EntityId);

                    if (armored > 0)
                        hits.Add(new ToolHit(target.EntityId, armored));

                    break;
                }

                case ActionId.ToolFieldRepair:
                {
                    // Armour on a player, health on a creature. repairtool.py refuses a dead
                    // player but allows a machina, so health is what brings a downed bot back
                    // and armour is what the tool does for a trooper. The hit data carries both
                    // slots either way and the client announces whichever is non-zero.
                    var isPlayer = EntityManager.Instance.Players.ContainsKey(target.EntityId);

                    var armored = isPlayer
                        ? ActorManager.Instance.RestoreArmor(target, amount, action.Actor.EntityId)
                        : 0;

                    var healed = isPlayer
                        ? 0
                        : ActorManager.Instance.Heal(target, amount, action.Actor.EntityId);

                    if (armored > 0 || healed > 0)
                        hits.Add(new ToolHit(target.EntityId, armored, healed));

                    break;
                }
            }

            // Sent even with nothing in it: the client's DoAction is what clears the action out
            // of __unresolvedActions, and an empty hit list simply announces nothing.
            CellManager.Instance.CellCallMethod(mapChannel, action.Actor,
                new ToolActionRecoveryPacket(action.ActionId, action.ActionArgId, hits));
        }

        private static Actor ResolveTarget(ulong entityId)
        {
            if (EntityManager.Instance.Players.TryGetValue(entityId, out var player))
                return player;

            return EntityManager.Instance.GetCreature(entityId);
        }

        private static WeaponClassInfo ClassInfoOf(Item tool)
        {
            if (tool?.ItemTemplate == null)
                return null;

            return EntityClassManager.Instance.LoadedEntityClasses
                .TryGetValue(tool.ItemTemplate.Class, out var entityClass)
                ? entityClass?.WeaponClassInfo
                : null;
        }

        private static void SendToOthers(MapChannel mapChannel, Actor actor, PythonPacket packet)
        {
            foreach (var cellSeed in actor.Cells)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    if (client.Player != actor)
                        client.CallMethod(actor.EntityId, packet);
        }

        /// <summary>
        /// The message this request should be refused with, or null if there is nothing wrong
        /// with it. Ordered as the client's own CheckAction chain is, so the player sees the same
        /// reason they would have seen had their client caught it first.
        /// </summary>
        private PlayerMessage? Validate(Client client, RequestToolActionPacket packet)
        {
            // An action id from outside the tool set did not come from a tool module, whatever
            // the client claims - the six are the complete set that can reach this opcode.
            if (!ToolActions.Contains(packet.ActionId))
                return PlayerMessage.PmCannotPerformActionNow;

            if (client.Player.State == CharacterState.Dead)
                return PlayerMessage.PmActionFailedActorDead;

            // basetoolaction.py: "if not actor.IsWeaponReady(): PM_CANNOT_PERFORM_ACTION_NOW"
            if (!client.Player.WeaponReady)
                return PlayerMessage.PmCannotPerformActionNow;

            var tool = InventoryManager.Instance.CurrentWeapon(client);

            // ...then "if not isinstance(tool, Weapon): PM_INVALID_WEAPON"
            if (tool?.ItemTemplate == null)
                return PlayerMessage.PmInvalidWeapon;

            var classInfo = ClassInfoOf(tool);

            if (classInfo == null)
                return PlayerMessage.PmInvalidWeapon;

            // The client picks the action module from the armed item's own class, so a genuine
            // request always names that item's action pair. Anything else is a client claiming to
            // swing a tool it is not holding - which is the whole reason the action id may not be
            // taken at its word when it decides what happens next.
            if (classInfo.WeaponAttackActionId != packet.ActionId || classInfo.WeaponAttackArgId != packet.ActionArgId)
                return PlayerMessage.PmInvalidWeapon;

            if (tool.IsJammed)
                return PlayerMessage.PmWeaponJammed;

            // basetoolaction.py checks ammo only when the tool has an ammo class at all - "if
            // tool.GetAmmoClassId() is not None and tool.GetCurrentAmmo() <= 0". A weapon class
            // with no ammo class stores 0; 294 of the 298 tool classes do have one.
            if (classInfo.AmmoClassId != 0 && tool.CurrentAmmo <= 0)
                return PlayerMessage.PmWeaponOutOfAmmo;

            return ValidateTarget(client, packet);
        }

        private PlayerMessage? ValidateTarget(Client client, RequestToolActionPacket packet)
        {
            // No tool sets TARGET_LOCATION, so a location here did not come from a tool module.
            if (packet.Target.Kind == ActionTargetKind.Location)
                return PlayerMessage.PmTargetInvalid;

            // An area tool sets TARGET_NONE and sends None; the cipher and the harvest tools
            // always need something to point at.
            if (!packet.Target.HasEntity)
            {
                return packet.ActionId switch
                {
                    ActionId.ToolHarvest => PlayerMessage.PmActionFailedNoTarget,
                    ActionId.ToolCipher => PlayerMessage.PmActionFailedNoTarget,
                    _ => null
                };
            }

            var targetId = packet.Target.EntityId;

            if (packet.ActionId == ActionId.ToolCipher)
                return ValidateCipherTarget(targetId);

            var creature = EntityManager.Instance.GetCreature(targetId);
            var player = EntityManager.Instance.Players.TryGetValue(targetId, out var targetPlayer)
                ? targetPlayer
                : null;

            if (creature == null && player == null)
                return PlayerMessage.PmActionFailedNoTarget;

            Actor targetActor = creature != null ? creature : player;

            if (packet.ActionId == ActionId.ToolHarvest)
                return ValidateHarvestTarget(client, packet, creature, targetId);

            // healdisc, repairtool, armoraug and nerfweapon are all TARGET_FRIENDLY or
            // TARGET_SELF. ToDo: target category - HOSTILE creatures should be refused here, but
            // faction and the client's target categories do not line up yet.

            // repairtool.py refuses a dead player outright; healdisc.py allows a corpse only at
            // Healing 3 or better. ToDo: repairtool also refuses dead BIOLOGICAL creatures, which
            // needs creature flags.
            if (targetActor.State == CharacterState.Dead)
            {
                if (packet.ActionId == ActionId.ToolFieldRepair && player != null)
                    return PlayerMessage.PmTargetInvalid;

                if (packet.ActionId == ActionId.ToolHealingDisc && !CanTargetCorpse(client))
                    return PlayerMessage.PmActionFailedTargetDead;

                if (packet.ActionId == ActionId.ToolArmorAugmentation || packet.ActionId == ActionId.ToolNerfweapon)
                    return PlayerMessage.PmActionFailedTargetDead;
            }

            return null;
        }

        private static PlayerMessage? ValidateHarvestTarget(Client client, RequestToolActionPacket packet,
            Creature creature, ulong targetId)
        {
            // harvest.py is TARGET_NON_SELF.
            if (targetId == client.Player.EntityId)
                return PlayerMessage.PmTargetInvalid;

            // "if not isinstance(target, _creature.Creature): PM_HARVEST_FAIL_NOT_HARVESTABLE",
            // and the same message again for a target that is not dead. Harvesting is done to a
            // corpse; a live one is not harvestable yet rather than invalid.
            if (creature == null || creature.State != CharacterState.Dead)
                return PlayerMessage.PmHarvestFailNotHarvestable;

            // ToDo: Salvage refuses a BIOLOGICAL creature and Tissue Extraction refuses a
            // MECHANICAL one, both with PmTargetInvalid. Creature flags are not loaded
            // server-side (CreatureManager still passes an empty flag list to CreatureInfo), so
            // neither can be checked. The arg id that decides which rule applies is already here:
            // 168 Salvage, 169 Tissue Extraction.
            if (packet.ActionArgId != (uint)SkillId.Salvage && packet.ActionArgId != (uint)SkillId.TissueExtraction)
                return PlayerMessage.PmHarvestFailNotHarvestable;

            return null;
        }

        private static PlayerMessage? ValidateCipherTarget(ulong targetId)
        {
            // cipher.py wants a Usable that IsCipherable(). Dynamic objects are the closest thing
            // the server has to a usable; ToDo: a cipherable flag, and CanCipher()'s skill check,
            // which would refuse with PmCipherFailSkillTooLow instead.
            if (!EntityManager.Instance.DynamicObjects.ContainsKey(targetId))
                return PlayerMessage.PmTargetInvalid;

            return null;
        }

        private static bool CanTargetCorpse(Client client)
        {
            return client.Player.Skills.TryGetValue(SkillId.Healing, out var healing)
                   && healing.SkillLevel >= CorpseTargetHealingSkill;
        }

        private static void Fail(Client client, RequestToolActionPacket packet, PlayerMessage? message)
        {
            client.CallMethod(client.Player.EntityId,
                new UserActionFailedPacket(packet.ActionId, packet.ActionArgId, message));
        }
    }
}
