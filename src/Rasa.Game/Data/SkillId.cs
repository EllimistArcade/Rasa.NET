namespace Rasa.Data
{
    public enum SkillId
    {
        None = -1,
        // reqruit Skill's
        Firearms = 2,
        HandToHand = 8,

        /// <summary>
        /// skilldata: T2_SPECIALIST_TOOLS = 14. The one skill behind everything in
        /// client/actions/tools/ - healdisc.py hardcodes the same number under the name
        /// HEALING_SKILL_ID, which is what this was called here until the cipher needed it and
        /// the two turned out to be the same skill.
        ///
        /// At level 3 or better healdisc.py lets the healing disc and the field repair tool be
        /// aimed at a corpse. All 22 cipher tool templates require it at level 1, and
        /// usable.py's CanCipher() reads the player's level in it against the lock's cipher
        /// level. The harvest tools require no skill at all.
        /// </summary>
        SpecialistTools = 14,

        MotorAssistArmor = 19,
        Lightning = 49,
        Sprint = 165
    }
}
