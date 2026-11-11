using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Rasa.Missions.Content;
using Rasa.Structures;

namespace Rasa.Services.Preloader
{
    using PlateauData = Missions.Plateau.PlateauMissionDataV1;
    using PoolsData = Missions.Pools.PoolsMissionDataV1;
    using Timora = Missions.Timora.TimoraMinesMissionsV1;
    using Torcastra = Missions.Torcastra.TorcastraMissionsV1;

    /// <summary>
    /// The instance missions' markers that were shown on the wrong map or on none (Instance_marker_maps).
    ///
    /// An objective's marker is sent only to a player on its map (MissionMarkerProjection): the scene's
    /// IndicatorMaps entry for it; else the scene's map; else the one map of the mission's areas, spawn
    /// groups and drops; else the one map its giver and receiver stand on. Found by an audit of every
    /// marker in the migrated world, each checked against the navmesh of the map it was sent on and
    /// against its mission's content:
    ///
    /// - Sent on the giver's map, the marker in an instance: Fuel Lake Sabotage (404) and Mining for
    ///   Information (1276), Timora Mines' pump and Eloh chamber, sent on the Divide; Deadly Silence
    ///   (1974), its four in Velon Hollow, sent on the Plateau.
    /// - The scene's map an instance, the marker outside it: One Way Trip (877), 9 and 13 on the
    ///   Plateau and 10 (Rapp) in Ustor Yard, sent on Maligo Base; Weapons Trafficking (904), 1 on the
    ///   Lamnas Armory door on the Pools; The Better Half (1975), 3 and 4 at Clark at the Wedge Rock;
    ///   Ancient Mysteries (1978), 2 at the Plateau's way into Velon Hollow and 3 at Hundra and Markin,
    ///   all on the Plateau but sent on Velon Hollow; Stolen History (1860, not enabled), 1 in Minos
    ///   Caverns and 2 in Timora Mines, sent on Torcastra.
    /// - Turpis Refinery's, sent there though on the Pools: Turpis Transport (1305), Corporal Hash; Looking
    ///   For Logos? (1308), 3, the refinery door.
    /// - On none: Reclaiming What's Ours (1056), objective 4's "Bane Generator" (243), which
    ///   Crater_lake_pen_generator added after Marker_map_scopes mapped the objective's other marker.
    ///
    /// Each named marker gets its map in the stored scene (mission_scene_binding.bindings): an
    /// indicatorMaps property added as the JSON's last, where the scene has none (as MarkerMapScopes);
    /// 243 added to Reclaiming What's Ours' objective 4 entry; and for Turpis Transport and Deadly
    /// Silence, which had no scene, one holding only these maps: no script, so nothing runs, and no
    /// requirement, items or credit, as a mission with no scene has. The mission's other markers are
    /// left to the map they had. Down takes off exactly what Up put in. The JSON is joined with || on
    /// SQLite and CONCAT on MySQL, where || is OR.
    /// </summary>
    public static class InstanceMarkerMaps
    {
        public const string Key = "\"indicatorMaps\":";

        public const uint Pools = 1304, Plateau = 1497, UstorYard = 1502, VelonHollow = 2136,
            TimoraMines = 1348, MinosCaverns = 1347, CraterLake = 1721;

        private static uint Unnamed(uint objective) => MissionIndicator.UnnamedFrom + objective;

        /// <summary>The markers given a map: (mission, revision, objective, indicator, map).</summary>
        public static readonly (uint MissionId, string Revision, uint Objective, uint Indicator, uint Map)[] Markers =
        {
            (404, Timora.Revision, 1, 26, TimoraMines),                     // Fuel Lake, the Main Fuel Pump
            (1276, Timora.Revision, 1, 384, TimoraMines),                   // Eloh Chamber
            (1276, Timora.Revision, 2, Unnamed(2), TimoraMines),
            (877, PlateauData.Revision, 9, Unnamed(1), Plateau),                // Ustor Yard's way in
            (877, PlateauData.Revision, 13, Unnamed(2), Plateau),               // Colonel Bosley
            (877, PlateauData.Revision, 10, Unnamed(3), UstorYard),             // Field Lt. Rapp
            (904, PlateauData.Revision, 1, Unnamed(1), Pools),                  // the Lamnas Armory door
            (1975, PlateauData.Revision, 3, Unnamed(3), Plateau),               // Clark, the Wedge Rock
            (1975, PlateauData.Revision, 4, Unnamed(4), Plateau),
            (1978, PlateauData.Revision, 2, Unnamed(1), Plateau),               // the Plateau's way into Velon Hollow
            (1978, PlateauData.Revision, 3, Unnamed(8), Plateau),               // Hundra, Markin
            (1978, PlateauData.Revision, 3, Unnamed(9), Plateau),
            (1308, PoolsData.Revision, 3, Unnamed(2), Pools),                   // Turpis Refinery's door
            (1860, Torcastra.Revision, 1, Unnamed(1), MinosCaverns),
            (1860, Torcastra.Revision, 2, Unnamed(2), TimoraMines),
            (1305, PoolsData.Revision, 1, Unnamed(1), Pools),                   // Corporal Hash
            (1974, PlateauData.Revision, 1, Unnamed(1), VelonHollow),           // Elder Hathen
            (1974, PlateauData.Revision, 2, Unnamed(2), VelonHollow),           // the three corrupted leaders
            (1974, PlateauData.Revision, 3, Unnamed(3), VelonHollow),
            (1974, PlateauData.Revision, 4, Unnamed(4), VelonHollow),
            (CraterLakeContent.ReclaimingWhatsOurs, CraterLakeContent.Revision, CraterLakeContent.UsePanel,
                CraterLakeContent.GeneratorMarker, CraterLake),
        };

        /// <summary>The missions that had no scene: one is put in holding only their markers' maps.</summary>
        public static readonly uint[] NewScenes = { 1305, 1974 };

        /// <summary>Reclaiming What's Ours' objective 4 entry as Marker_map_scopes left it, and with 243.</summary>
        public static readonly string ReclaimingEntry = "\"4\":{\"1000000004\":1721}",
            ReclaimingEntryWithGenerator = "\"4\":{\"1000000004\":1721,\"243\":1721}";

        private static IEnumerable<(uint MissionId, string Revision)> Appended() =>
            Markers.Where(m => m.MissionId != CraterLakeContent.ReclaimingWhatsOurs && !NewScenes.Contains(m.MissionId))
                .Select(m => (m.MissionId, m.Revision)).Distinct();

        private static string RevisionOf(uint missionId) => Markers.First(m => m.MissionId == missionId).Revision;

        /// <summary>A mission's markers' maps, as MissionContentCodec writes a scene's indicatorMaps.</summary>
        public static Dictionary<uint, Dictionary<uint, uint>> Maps(uint missionId) =>
            Markers.Where(m => m.MissionId == missionId).GroupBy(m => m.Objective)
                .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Indicator, m => m.Map));

        public static string MapsJson(uint missionId) => JsonSerializer.Serialize(Maps(missionId), MissionContentCodec.Options);

        /// <summary>The scene put in for a mission that had none.</summary>
        public static MissionSceneDefinition NewScene(uint missionId) => new() { IndicatorMaps = Maps(missionId) };

        public static string NewSceneJson(uint missionId) => JsonSerializer.Serialize(NewScene(missionId), MissionContentCodec.Options);

        private static string Suffix(uint missionId) => $",{Key}{MapsJson(missionId)}}}";

        public static IEnumerable<string> Up(bool mySql)
        {
            foreach (var (missionId, revision) in Appended())
            {
                var head = "SUBSTR(bindings, 1, LENGTH(bindings) - 1)";
                var joined = mySql ? $"CONCAT({head}, {Q(Suffix(missionId))})" : $"{head} || {Q(Suffix(missionId))}";
                yield return $"UPDATE mission_scene_binding SET bindings = {joined} " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} " +
                    $"AND bindings LIKE '%}}' AND bindings NOT LIKE '%{Key}%';";
            }

            yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(ReclaimingEntry)}, {Q(ReclaimingEntryWithGenerator)}) " +
                $"WHERE mission_id = {CraterLakeContent.ReclaimingWhatsOurs} AND content_revision = {Q(CraterLakeContent.Revision)} " +
                $"AND bindings LIKE {Q("%" + ReclaimingEntry + "%")};";

            foreach (var missionId in NewScenes)
            {
                var scene = NewScene(missionId);
                yield return "INSERT INTO mission_scene_binding (mission_id, content_revision, script_key, state_version, bindings) " +
                    $"SELECT d.mission_id, d.content_revision, NULL, {scene.StateVersion}, {Q(NewSceneJson(missionId))} " +
                    $"FROM mission_content_definition d WHERE d.mission_id = {missionId} AND d.content_revision = {Q(RevisionOf(missionId))} " +
                    "AND NOT EXISTS (SELECT 1 FROM mission_scene_binding x WHERE x.mission_id = d.mission_id AND x.content_revision = d.content_revision);";
            }
        }

        public static IEnumerable<string> Down()
        {
            foreach (var missionId in NewScenes)
                yield return $"DELETE FROM mission_scene_binding WHERE mission_id = {missionId} AND content_revision = {Q(RevisionOf(missionId))} " +
                    $"AND bindings = {Q(NewSceneJson(missionId))};";

            yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(ReclaimingEntryWithGenerator)}, {Q(ReclaimingEntry)}) " +
                $"WHERE mission_id = {CraterLakeContent.ReclaimingWhatsOurs} AND content_revision = {Q(CraterLakeContent.Revision)} " +
                $"AND bindings LIKE {Q("%" + ReclaimingEntryWithGenerator + "%")};";

            foreach (var (missionId, revision) in Appended())
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(Suffix(missionId))}, '}}') " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} AND bindings LIKE {Q("%" + Suffix(missionId))};";
        }

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
