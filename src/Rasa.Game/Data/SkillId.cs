namespace Rasa.Data
{
    public enum SkillId
    {
        None = -1,
        // reqruit Skill's
        Firearms = 2,
        HandToHand = 8,

        /// <summary>
        /// client/actions/tools/healdisc.py: HEALING_SKILL_ID = 14. At level 3 or better the
        /// healing disc and the field repair tool may be aimed at a corpse.
        /// </summary>
        Healing = 14,

        MotorAssistArmor = 19,
        Lightning = 49,
        Sprint = 165,

        /// <summary>
        /// client/actions/tools/harvest.py: SKILL_SALVAGE = 168. Also the arg id TOOL_HARVEST
        /// carries when the action is a salvage rather than a tissue extraction.
        /// </summary>
        Salvage = 168,

        /// <summary>client/actions/tools/harvest.py: SKILL_TISSUE_EXTRACTION = 169.</summary>
        TissueExtraction = 169
    }
}
