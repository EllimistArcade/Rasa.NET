namespace Rasa.Managers
{
    using Data;
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
