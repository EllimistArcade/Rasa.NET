using System.Collections.Generic;

using F = Rasa.Services.Preloader.Missions.Plateau.PlateauFinalAssaultV1;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Final Assault's power core the right way round (Final_assault_core). Its model,
    /// UsableTwoStateBaneIndustrialReactorRoomSunV01, is the running reactor sun in state 56 (TS_STATE_1), plays its
    /// destruction with its effect going to 55 (TS_STATE_0), and is the destroyed core in 55. The scene had it ready
    /// in 55 and set in 56: the core stood destroyed until the detonator went on it, which brought it back to life.
    ///
    /// The scene is the stored Final Assault scene (mission_scene_binding.bindings, camelCase as MissionContentCodec
    /// writes it): the core's initial state and the state its arming transition sets are replaced, each in a
    /// fragment found once in it. Guarded: a scene not as this found it is left as it is, both ways.
    /// </summary>
    public static class FinalAssaultCore
    {
        public const uint Mission = F.MissionId;
        public const string Revision = Missions.Plateau.PlateauMissionDataV1.Revision;

        /// <summary>The core's initial state in the scene: as it was, and as it is now.</summary>
        public static readonly string InitialWas = Initial(F.CoreSetState), InitialNow = Initial(F.CoreReadyState);

        /// <summary>The state the core's arming sets: as it was, and as it is now.</summary>
        public static readonly string ArmWas = Arm(F.CoreReadyState), ArmNow = Arm(F.CoreSetState);

        public static IEnumerable<string> Up()
        {
            // Both found, or neither changes: the two are replaced in the one statement.
            yield return Replace(InitialWas, InitialNow, ArmWas, ArmNow);
        }

        public static IEnumerable<string> Down()
        {
            yield return Replace(InitialNow, InitialWas, ArmNow, ArmWas);
        }

        private static string Initial(uint state) =>
            $"\"initialObjectState\":{state},\"missionId\":0,\"spawnId\":0,\"useAction\":{{\"missionId\":{F.MissionId},";

        private static string Arm(uint state) =>
            $"\"state\":{state},\"windupMilliseconds\":0,\"operationKey\":\"arm-power-core\"";

        private static string Replace(string firstFrom, string firstTo, string secondFrom, string secondTo) =>
            $"UPDATE mission_scene_binding SET bindings = REPLACE(REPLACE(bindings, '{firstFrom}', '{firstTo}'), '{secondFrom}', '{secondTo}') " +
            $"WHERE mission_id = {Mission} AND content_revision = '{Revision}' " +
            $"AND bindings LIKE '%{firstFrom}%' AND bindings LIKE '%{secondFrom}%';";
    }
}
