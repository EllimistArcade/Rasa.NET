using System.Collections.Generic;

using Rasa.Services.Preloader.Missions.Wilderness;
using Rasa.Structures.World;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// "Getting It In Gear: Soldier Class" (2010) and "Specialist Class" (2011), the class equipment missions, cannot
    /// be abandoned (Class_equipment_missions_kept). The client's shared/gameconstants.py has
    /// <c>NON_ABANDONABLE_MISSIONS = [1990, 2010, 2011]</c> and hides the Abandon button for them
    /// (currentmissionswindow.py); 1990 was already Prohibited (WorldContentDataV1), and these two were put in Allowed
    /// (WildernessAliaBranchesV1), so the server let a modified client abandon them.
    ///
    /// Plain SQL for both providers. Guarded: a row not as this found it is left as it is, both ways.
    /// </summary>
    public static class ClassEquipmentMissionsKept
    {
        public const uint Soldier = 2010, Specialist = 2011;

        public static readonly uint[] Missions = { Soldier, Specialist };

        public const string Revision = WildernessMissionDataV1.Revision;

        public static IEnumerable<string> Up()
        {
            yield return Set(MissionAbandonmentPolicy.Allowed, MissionAbandonmentPolicy.Prohibited);
        }

        public static IEnumerable<string> Down()
        {
            yield return Set(MissionAbandonmentPolicy.Prohibited, MissionAbandonmentPolicy.Allowed);
        }

        private static string Set(MissionAbandonmentPolicy from, MissionAbandonmentPolicy to) =>
            $"UPDATE mission_content_definition SET abandonment_policy = {(byte)to} " +
            $"WHERE mission_id IN ({Soldier}, {Specialist}) AND content_revision = '{Revision}' AND abandonment_policy = {(byte)from};";
    }
}
