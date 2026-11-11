namespace Rasa.Managers
{
    using Data;
    using Structures;

    /// <summary>
    /// What EMP does to a creature by its kind, from the constants the client keeps for the
    /// server (shared/gameconstants.py), which nothing in the client reads:
    ///
    ///  - EMP_DAMAGE_MODIFIER_BIOLOGICAL = 0.0: nothing to a BIOLOGICAL creature;
    ///  - EMP_DAMAGE_MODIFIER_MACHINA = 0.5: half to a MACHINA;
    ///  - EMP_DAMAGE_MODIFIER_MECHANICAL = 1.0: all of it to a MECHANICAL one.
    ///
    /// The kind is the creature flag on its class or on itself (CreatureManager.HasFlag); the
    /// client has the three as one each (CreatureFlag). A creature of none of them - a NeoBot,
    /// a Warden, an NPC with no species - takes EMP as it did before, whole. Players are not
    /// creatures and take it whole, through their armour's EMP resistance.
    ///
    /// The one class with a kind and a word of its own about EMP is the Bane Stalker
    /// (Vehicle_Bane_Stalker): BIOLOGICAL and VULNERABLE_EMP. A vulnerability to EMP says EMP
    /// hurts it, so a creature that carries VULNERABLE_EMP takes it as a machine does, and its
    /// vulnerability on top (CreatureResistances), not nothing. Ours.
    ///
    /// The factor is on a creature's health alone: EMP goes against shields and armour, and those
    /// of a biological creature or a Machina take it whole. A hit is resisted as any other
    /// (GameEffectManager.ApplyResist), a shield takes its share (ApplyAbsorb, ShieldDrone), the
    /// armour its own, and what is left for health is cut by the factor (ToHealth) - a living
    /// creature's armour is stripped by EMP and its health untouched. What the factor spares is
    /// not counted as dealt (ActorManager.Damage, MissileManager).
    /// </summary>
    public static class EmpDamage
    {
        public const double Biological = 0.0;   // EMP_DAMAGE_MODIFIER_BIOLOGICAL
        public const double Machina = 0.5;      // EMP_DAMAGE_MODIFIER_MACHINA
        public const double Mechanical = 1.0;   // EMP_DAMAGE_MODIFIER_MECHANICAL

        /// <summary>The share of a hit of this type the target takes for its kind: 1 but for EMP on a biological creature or a Machina.</summary>
        public static double FactorFor(Actor target, DamageType damageType)
        {
            if (damageType != DamageType.EMP || target is not Creature creature)
                return 1.0;

            if (CreatureManager.HasFlag(creature, CreatureFlag.VulnerableEmp))
                return Mechanical;

            if (CreatureManager.HasFlag(creature, CreatureFlag.Biological))
                return Biological;

            if (CreatureManager.HasFlag(creature, CreatureFlag.Machina))
                return Machina;

            return Mechanical;
        }

        /// <summary>What of a hit that got past the shields and armour reaches the target's health, by its kind.</summary>
        public static int ToHealth(Actor target, DamageType damageType, int pastArmor)
        {
            var factor = FactorFor(target, damageType);

            return factor >= 1.0 || pastArmor <= 0 ? pastArmor : (int)System.Math.Round(pastArmor * factor);
        }
    }
}
