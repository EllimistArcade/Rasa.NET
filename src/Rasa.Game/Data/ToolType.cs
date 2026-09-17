namespace Rasa.Data
{
    /// <summary>
    /// itemtemplate_weapon.tool_type, from generated/client/constant/tooltype - the client's own
    /// twelve-value table, which is the whole of it. It is not a weapon taxonomy: the game has
    /// fourteen weapon families and eight of them have no entry here at all.
    ///
    /// The client reads it in two places.
    ///
    /// The tooltip, at index 15 of the weapon block (gameuiutil.kWeaponIdx_ToolType), which
    /// ItemTemplateTooltipInfo carries. <c>IsTool()</c> is true for 1-6 and suppresses the
    /// ordinary damage line, and <c>_AddWeaponToolInfo</c> turns the value into the line that
    /// replaces it - one string per tool, in the client's own words: "Healing: N",
    /// "Armor Recharge: N", "Decode Chance: N%", "Extraction Chance: N%", "Salvage Chance: N%",
    /// "Repair: N". A value outside 1-12 passes the <c>toolType &gt; 0</c> guard, matches no
    /// branch and inserts a blank line, which is what the placeholder 15 was doing on every
    /// weapon in the game.
    ///
    /// And reload speed, at field 14 of WeaponInfo: actions/weaponreload.py hands it to
    /// <c>GetReloadModifier(skillId, typeId)</c> on the SKILL_LIMITED_BY_TYPE_RELOAD_MODIFIER and
    /// MODULE_MODIFY_RELOAD effects, which return 0 for a type they do not list. So a module that
    /// speeds up reloading for one weapon family needs this to name that family.
    /// </summary>
    public enum ToolType
    {
        /// <summary>
        /// No entry in the table. Correct for the eight families the enum does not name -
        /// machine guns, staves, blades, torqueshell, net, polarity, injection and propellant
        /// guns - and for creature and test weapons.
        /// </summary>
        None = 0,

        HealingDisc = 1,
        ArmorAug = 2,
        Cipher = 3,
        TissueExtractor = 4,
        Salvage = 5,
        FieldRepair = 6,
        Rifle = 7,
        Pistol = 8,
        Shotgun = 9,
        GrenadeLauncher = 10,
        RocketLauncher = 11,

        /// <summary>
        /// The family the entity classes call LeechGun and the skill tree calls
        /// T2_SPECIALIST_LEECH_GUN. Its attack action is WeaponDensitygun, and
        /// ID_TOOLTIP_CS_DENSITY_FIELD_GUNS_30 reads "Leech Guns up to Lvl 30" - five names, one
        /// family.
        /// </summary>
        DensityGun = 12
    }
}
