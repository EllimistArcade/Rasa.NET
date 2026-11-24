using System.Collections.Generic;
using System.Text.Json;
using Rasa.Missions.Content;
using Rasa.Missions.Definitions;
using Rasa.Services.Preloader.Missions;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Lightbender Glands (795) takes its glands from the Wilderness's Light Bender Snipers too
    /// (Lightbender_gland_snipers).
    ///
    /// Dr. Ojy asks for four glands from "the Bane's Light Benders" and the mission gives no
    /// place. The Wilderness has two kinds of ordinary Lightbender: the reconstructed camp
    /// (creature 630071, pools 630071..630074) and the Light Bender Snipers (creature 630046,
    /// native name 406, pools 630046..630051). The gland drop named only the camp, so a sniper's
    /// corpse never held a gland, though it is the same class (7120) and its name is the very
    /// "Light Bender" the mission asks for. The drop now names both. Proctor Fulgor (76), a named
    /// boss, still doesn't drop one.
    ///
    /// Plain SQL, valid on SQLite and MySQL alike: the binding's drop changes only while it is
    /// the one Wilderness_landing_zone wrote, and Down puts that back.
    /// </summary>
    public static class LightbenderGlandSnipers
    {
        public const uint MissionId = 795, Objective = 3, Map = 1220;
        public const uint Camp = 630071, Sniper = 630046;
        public const string Revision = "wilderness_1_16_5";

        public static MissionItemDropDefinition Before() => new(new[] { Camp }, Objective, Map, 100, 1);

        public static MissionItemDropDefinition After() => new(new[] { Camp, Sniper }, Objective, Map, 100, 1);

        public static IEnumerable<string> Up()
        {
            yield return Swap(Before(), After());
        }

        public static IEnumerable<string> Down()
        {
            yield return Swap(After(), Before());
        }

        private static string Swap(MissionItemDropDefinition from, MissionItemDropDefinition to)
        {
            var was = InstanceMissionSql.S(Json(from));
            return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {was}, {InstanceMissionSql.S(Json(to))}) " +
                $"WHERE mission_id = {MissionId} AND content_revision = '{Revision}' AND INSTR(bindings, {was}) > 0;";
        }

        internal static string Json(MissionItemDropDefinition drop) =>
            "\"drop\":" + JsonSerializer.Serialize(drop, MissionContentCodec.Options);
    }
}
