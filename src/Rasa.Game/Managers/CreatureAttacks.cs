using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Packets;
    using Packets.MapChannel.Server.PerformRecovery;
    using Structures;

    /// <summary>
    /// What a creature's attack lands as. Nothing in the client marks a creature with a damage
    /// type: the type comes from the weapon the attack plays, and an attack names its weapon by
    /// the action and action argument it is performed with. So the type is looked for in three
    /// places, in order:
    ///
    ///  1. The creature_action row, when it states one. Content can say what an attack deals
    ///     whatever the client data implies.
    ///  2. The action's own DAMAGE_TYPE property, for the creature abilities that carry one -
    ///     CR_MOX_ENERGY_ATTACK is electrical in its ability data, and any creature ability wired
    ///     later brings its type with it.
    ///  3. CreatureWeaponDamage, the pair table mined from the client's weapon classes.
    ///
    /// Anything left over is physical, which is what every creature attack was until now. The
    /// answer is kept on the action so it is worked out once.
    /// </summary>
    public static class CreatureAttacks
    {
        public static DamageType DamageTypeOf(CreatureAction action)
        {
            if (action.DamageType != 0)
                return (DamageType)action.DamageType;

            if (action.ResolvedDamageType != 0)
                return action.ResolvedDamageType;

            var resolved = Resolve(action);

            action.ResolvedDamageType = resolved;

            return resolved;
        }

        /// <summary>The player modules creature actions use whose class is DamageBase: hitdata (rawInfo, onHitData).</summary>
        private static readonly HashSet<string> DamageBaseModules = new HashSet<string>
        {
            "abilities.knockback", "abilities.stun", "abilities.tectonicstrike", "abilities.shrapnel",
            "abilities.deathdamage", "abilities.rushingblow"
        };

        public const string LightningModule = "abilities.lightning";
        public const string KaelRushingBlowModule = "abilities.ai.kaelrushingblowability";
        public const string LinkerHandBlastModule = "abilities.ai.linkerhandblastability";

        /// <summary>
        /// The hitdata shape an action's client class unpacks, by its module (RecoveryShape).
        /// No module, or a weapon's, is the weapon shape; a creature class under abilities.ai
        /// that does not say otherwise indexes a bare rawInfo, as does a class with no DoAbility
        /// of its own, which reads nothing.
        /// </summary>
        public static RecoveryShape ShapeOfModule(string module)
        {
            if (string.IsNullOrEmpty(module) || module.StartsWith("weapons."))
                return RecoveryShape.Weapon;

            if (module == LightningModule)
                return RecoveryShape.DamageArcs;

            if (DamageBaseModules.Contains(module))
                return RecoveryShape.Damage;

            if (module == KaelRushingBlowModule)
                return RecoveryShape.EntityRawInfo;

            if (module == LinkerHandBlastModule)
                return RecoveryShape.Drain;

            return RecoveryShape.RawInfo;
        }

        /// <summary>The recovery for a missile: a creature's ability in its class's shape, anything else as a weapon attack.</summary>
        public static ServerPythonPacket RecoveryFor(Missile missile)
        {
            if (missile.Source is Creature && AbilityManager.Instance != null
                && AbilityManager.Instance.TryGetAction(missile.ActionId, missile.ActionArgId, out var module, out _))
            {
                var shape = ShapeOfModule(module);

                if (shape != RecoveryShape.Weapon)
                    return new CreatureAbilityRecovery(missile, shape);
            }

            return new WeaponAttackRecovery(missile);
        }

        private static DamageType Resolve(CreatureAction action)
        {
            if (AbilityManager.Instance.TryGetLevel(action.ActionId, action.ActionArgId, out var level))
            {
                var stated = level.Get(AbilityProperty.DamageType);

                if (stated != 0)
                    return (DamageType)stated;
            }

            return CreatureWeaponDamage.Of((uint)action.ActionId, action.ActionArgId);
        }
    }
}
