using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
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
    /// Nothing here has an effect yet. Every request that survives validation is refused with
    /// PmCannotPerformActionNow, which is honest - the tool really cannot do anything - and,
    /// unlike silence, leaves the client's action bookkeeping in a clean state. What the
    /// validation is for is that it is the half that has to be right either way: the client's own
    /// CheckAction is a courtesy to the player, not a constraint on the wire, and every rule below
    /// has to hold server-side before any tool is allowed to do anything at all.
    ///
    /// Two checks the client makes cannot be made here yet, and are marked ToDo rather than
    /// guessed at: creature flags (BIOLOGICAL / MECHANICAL / MACHINA) are not loaded server-side,
    /// and there is no cipherable flag on dynamic objects. Both refuse conservatively for now.
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
        /// The healing skill level at which healdisc.py sets <c>canTargetDead</c>, letting the
        /// healing disc and field repair tool be aimed at a corpse.
        /// </summary>
        public const int CorpseTargetHealingSkill = 3;

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
                            _instance = new ToolActionManager();
                    }
                }

                return _instance;
            }
        }

        public void RequestToolAction(Client client, RequestToolActionPacket packet)
        {
            if (client?.Player == null || client.State != ClientState.Ingame)
                return;

            var refusal = Validate(client, packet);

            // A request that survives validation is still refused, because nothing reaches the
            // tools themselves yet - but silently, with msgId None. The client cancels the action
            // and pops it off __unresolvedActions either way; what it skips is the message. That
            // is deliberate: the player did nothing wrong, and "You cannot perform that action
            // right now" on every click of a tool that is simply not finished tells them less
            // than nothing. When the effects land, this is the line that becomes the work.
            Fail(client, packet, refusal);
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

            var classInfo = EntityClassManager.Instance.LoadedEntityClasses
                .TryGetValue(tool.ItemTemplate.Class, out var entityClass)
                ? entityClass?.WeaponClassInfo
                : null;

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
