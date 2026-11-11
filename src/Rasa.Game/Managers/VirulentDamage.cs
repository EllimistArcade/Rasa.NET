using System;

namespace Rasa.Managers
{
    using Data;
    using Structures;

    /// <summary>
    /// Virulent damage. The sources: "Armor piercing (but effect reduced by armor state).
    /// Machines are immune. Crit triggers a snare effect." - it does not drain the armour bar
    /// first but goes straight to health, and the more of its armour the target still has, the
    /// more of it is held off; so it is weak as an opener on a well-armoured foe and deadly once
    /// EMP or Physical has stripped the armour.
    ///
    /// The client keeps the number for the server beside the EMP modifiers (shared/
    /// gameconstants.py), which nothing in the client reads: VIRULENT_RESIST_FROM_ARMOR_MOD = 0.75.
    /// Read here as the share of a hit full armour holds off, falling in a straight line with the
    /// armour left: at full armour a quarter gets through, at half five eighths, with none all of
    /// it. The straight line is ours.
    ///
    /// - What the armour holds off is resisted (GameEffectManager.ApplyResist, on top of the
    ///   target's resistance to Virulent), and the hit floats it as such.
    /// - The armour bar is not touched (ActorManager.Damage, MissileManager). A shield still
    ///   takes its share.
    /// - A MECHANICAL creature is immune (DamageImmunity, through CreatureImmunities), as one
    ///   with IMMUNE_VIRULENT is; one that carries VULNERABLE_VIRULENT (the Bane Linker) says
    ///   Virulent hurts it, and takes it with its vulnerability, as the Bane Stalker takes EMP.
    ///   A Machina is not a machine and takes it.
    /// - The crit's snare is CritEffects' Virulent cripple.
    /// </summary>
    public static class VirulentDamage
    {
        /// <summary>VIRULENT_RESIST_FROM_ARMOR_MOD: the share of a Virulent hit full armour holds off.</summary>
        public const double ResistFromArmorMod = 0.75;

        /// <summary>Whether a hit of this type goes past the armour bar to health.</summary>
        public static bool BypassesArmor(DamageType damageType) => damageType == DamageType.Virulent;

        /// <summary>The share of a hit of this type the target's armour lets through: 1 but for Virulent on a target with armour left.</summary>
        public static double ArmorFactor(Actor target, DamageType damageType)
        {
            if (damageType != DamageType.Virulent || target == null || !target.Attributes.TryGetValue(Attributes.Armor, out var armor) || armor.CurrentMax <= 0)
                return 1.0;

            var left = Math.Clamp((double)armor.Current / armor.CurrentMax, 0.0, 1.0);

            return 1.0 - ResistFromArmorMod * left;
        }

        /// <summary>Whether the creature is a machine Virulent does nothing to: MECHANICAL, and not VULNERABLE_VIRULENT.</summary>
        public static bool SparesMachine(Creature creature) =>
            CreatureManager.HasFlag(creature, CreatureFlag.Mechanical) && !CreatureManager.HasFlag(creature, CreatureFlag.VulnerableVirulent);
    }
}
