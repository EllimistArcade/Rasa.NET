namespace Rasa.Data
{
    /// <summary>
    /// The only argument of DisplayPlayerTutorialNotification, from
    /// generated/client/tutorialdata - the client's own table, names and all.
    ///
    /// The client owns every tutorial's text, artwork and audio; the server picks which one to
    /// raise and nothing more. Both id ranges are the client's own: 1-9 were added after the
    /// 10000000 block, and both are live.
    ///
    /// The doc comment on each is the client's internal name for that tutorial, which is the
    /// only description of it that exists outside the language files.
    /// </summary>
    public enum TutorialId
    {
        /// <summary>rezsickness</summary>
        Rezsickness = 1,

        /// <summary>prestige_gained</summary>
        PrestigeGained = 2,

        /// <summary>clone_credit_added</summary>
        CloneCreditAdded = 3,

        /// <summary>control_point</summary>
        ControlPoint = 4,

        /// <summary>trays</summary>
        Trays = 5,

        /// <summary>tuthealthpower</summary>
        Tuthealthpower = 6,

        /// <summary>map_window_closed</summary>
        MapWindowClosed = 7,

        /// <summary>ability_pump</summary>
        AbilityPump = 8,

        /// <summary>battleground_rules_intro</summary>
        BattlegroundRulesIntro = 9,

        /// <summary>tutminimap2</summary>
        Tutminimap2 = 10000000,

        /// <summary>tutforcefield</summary>
        Tutforcefield = 10000001,

        /// <summary>tutlevelup</summary>
        Tutlevelup = 10000002,

        /// <summary>tuthospital</summary>
        Tuthospital = 10000003,

        /// <summary>tutexplore</summary>
        Tutexplore = 10000004,

        /// <summary>tutminimap</summary>
        Tutminimap = 10000005,

        /// <summary>tutpathbad</summary>
        Tutpathbad = 10000006,

        /// <summary>tutpath3</summary>
        Tutpath3 = 10000007,

        /// <summary>tutcrouch</summary>
        Tutcrouch = 10000008,

        /// <summary>tuttarget</summary>
        Tuttarget = 10000009,

        /// <summary>tuthealth</summary>
        Tuthealth = 10000010,

        /// <summary>tutmissiongiver</summary>
        Tutmissiongiver = 10000011,

        /// <summary>tutbunk</summary>
        Tutbunk = 10000012,

        /// <summary>tutfootlocker</summary>
        Tutfootlocker = 10000013,

        /// <summary>reload</summary>
        Reload = 10000014,

        /// <summary>tutabilities</summary>
        Tutabilities = 10000015,

        /// <summary>tutfire</summary>
        Tutfire = 10000016,

        /// <summary>tutinventoryability</summary>
        Tutinventoryability = 10000017,

        /// <summary>tutctrl</summary>
        Tutctrl = 10000018,

        /// <summary>tutrun</summary>
        Tutrun = 10000019,

        /// <summary>tutmissions</summary>
        Tutmissions = 10000020,

        /// <summary>tutwaypoints</summary>
        Tutwaypoints = 10000021,

        /// <summary>tutswitchweapons</summary>
        Tutswitchweapons = 10000022,

        /// <summary>tutinventorytotray</summary>
        Tutinventorytotray = 10000023,

        /// <summary>tutmovement</summary>
        Tutmovement = 10000024,

        /// <summary>tutspkwcpt</summary>
        Tutspkwcpt = 10000025,

        /// <summary>tutuseshifter</summary>
        Tutuseshifter = 10000026,

        /// <summary>tutfallingdmg</summary>
        Tutfallingdmg = 10000027,

        /// <summary>tutorial1</summary>
        Tutorial1 = 10000028,

        /// <summary>levelup</summary>
        Levelup = 10000029,

        /// <summary>weapon_jammed</summary>
        WeaponJammed = 10000030,

        /// <summary>lockbox</summary>
        Lockbox = 10000031,

        /// <summary>clan_registrar</summary>
        ClanRegistrar = 10000032,

        /// <summary>clan_invite</summary>
        ClanInvite = 10000033,

        /// <summary>squad_invite</summary>
        SquadInvite = 10000034,

        /// <summary>duel</summary>
        Duel = 10000035,

        /// <summary>crafting</summary>
        Crafting = 10000036,

        /// <summary>no_ammo</summary>
        NoAmmo = 10000037,

        /// <summary>waypoint</summary>
        Waypoint = 10000038,

        /// <summary>broken_equipment</summary>
        BrokenEquipment = 10000039,

        /// <summary>clanlockbox</summary>
        Clanlockbox = 10000040,

        /// <summary>killstreak</summary>
        Killstreak = 10000043,

        /// <summary>critdeath</summary>
        Critdeath = 10000044,

        /// <summary>logosadded</summary>
        Logosadded = 10000045,

        /// <summary>status_effect_gained</summary>
        StatusEffectGained = 10000046,

        /// <summary>instance_entered</summary>
        InstanceEntered = 10000047,

        /// <summary>minion_summoned</summary>
        MinionSummoned = 10000048,
    }
}
