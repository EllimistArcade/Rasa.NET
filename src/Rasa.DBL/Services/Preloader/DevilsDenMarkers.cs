using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using Rasa.Missions.Content;
using Rasa.Structures;
using Rasa.Structures.World;

using D = Rasa.Services.Preloader.DevilsDenContent;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The Devil's Den missions' map markers (Devils_den_markers).
    ///
    /// Each marker is shown only on its map: the scene's IndicatorMaps entry for it, else the
    /// mission's default (MissionMarkerProjection), which for a scene with no map is the one map its
    /// giver and receiver stand on. The Den's scenes name no map and their givers stand at Hightower
    /// Outpost in the Palisades, so the markers at Den places were sent on the Palisades (1244), at
    /// Den coordinates, and in the Den (1394) not at all; Find Caritas and Finish the Task, with a
    /// giver and receiver on different maps, had none anywhere. Each marker now names its map: the
    /// Den, or the Palisades for the three rangers at Hightower (Kogari, Brody, Cyrida).
    ///
    /// Sever the Devil's Head (672), objective 3, the three Thrax Machinery Power Nodes, also gets a
    /// second marker, on the third node west of the Shard Impact Site (D.NodeCAt). Its one marker,
    /// 15 m round the first node in the tower's service room, takes in the second (14 m off) but not
    /// the third, 165 m away and reached from the landing pad. Indicator id <see cref="NodeCMarker"/>,
    /// unnamed (past objective 3's own); radius an object's, 8 m.
    ///
    /// The maps go into the stored scenes (mission_scene_binding.bindings, camelCase as
    /// MissionContentCodec writes it) as the last property of the JSON, only where the scene has no
    /// indicatorMaps; Down takes off exactly what Up added. The JSON is joined with || on SQLite and
    /// CONCAT on MySQL, where || is OR (as BoundSceneMaps).
    /// </summary>
    public static class DevilsDenMarkers
    {
        public const string Key = "\"indicatorMaps\":";

        public const uint NodeObjective = 3;
        public const uint NodeCMarker = MissionIndicator.UnnamedFrom + 1003;
        public const double NodeCRadius = 8;

        private const uint Den = D.Den, Palisades = D.Palisades;

        private static uint Unnamed(uint objective) => MissionIndicator.UnnamedFrom + objective;

        /// <summary>Every Den mission marker: (mission, objective, indicator, map).</summary>
        public static readonly (uint MissionId, uint Objective, uint Indicator, uint Map)[] Markers =
        {
            (D.HakarisBand, 1, Unnamed(1), Den),
            (D.LuroronsBand, 1, Unnamed(1), Den),
            (D.FindCaritas, 1, Unnamed(1), Den),
            (D.ElohArtifacts, 1, Unnamed(1), Den),
            (D.ElohArtifacts, 2, Unnamed(2), Palisades),         // Kogari
            (D.DevilsStrike, 1, Unnamed(1), Den),
            (D.DevilsStrike, 2, Unnamed(2), Den),
            (D.MissingStrikeTeam, 1, Unnamed(1), Den),
            (D.MissingStrikeTeam, 2, Unnamed(2), Palisades),     // Brody
            (D.FinishTheTask, 3, Unnamed(3), Den),
            (D.FinishTheTask, 7, Unnamed(7), Palisades),         // Cyrida
            (D.FinishTheTask, 8, Unnamed(8), Den),
            (D.SeverTheHead, 1, D.FieldCommanderMarker, Den),
            (D.SeverTheHead, 3, Unnamed(3), Den),
            (D.SeverTheHead, NodeObjective, NodeCMarker, Den),
            (D.SeverTheHead, 4, D.LandingPadMarker, Den),
            (D.SeverTheHead, 5, D.FuelDepotMarker, Den),
        };

        /// <summary>A mission's indicatorMaps, as MissionContentCodec writes the scene's.</summary>
        public static string MapsJson(uint missionId) =>
            JsonSerializer.Serialize(Markers.Where(m => m.MissionId == missionId).GroupBy(m => m.Objective)
                .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Indicator, m => m.Map)), MissionContentCodec.Options);

        private static IEnumerable<uint> Missions => Markers.Select(m => m.MissionId).Distinct();

        private static string Suffix(uint missionId) => $",{Key}{MapsJson(missionId)}}}";

        public static IEnumerable<string> Up(bool mySql)
        {
            var at = D.NodeCAt;
            yield return "INSERT INTO mission_indicator (mission_id, content_revision, requirement, objective_id, indicator_id, pos_x, pos_y, " +
                "pos_z, radius, show_3d_effect, comment) VALUES (" +
                string.Join(", ", D.SeverTheHead, Quote(D.Revision), (byte)MissionContentRequirement.Required,
                    NodeObjective, NodeCMarker, N(at.X), N(at.Y), N(at.Z), N(NodeCRadius), 0, Quote("Power node west of the impact site")) + ");";

            foreach (var missionId in Missions)
            {
                var head = "SUBSTR(bindings, 1, LENGTH(bindings) - 1)";
                var joined = mySql ? $"CONCAT({head}, {Quote(Suffix(missionId))})" : $"{head} || {Quote(Suffix(missionId))}";
                yield return $"UPDATE mission_scene_binding SET bindings = {joined} " +
                    $"WHERE mission_id = {missionId} AND content_revision = '{D.Revision}' " +
                    $"AND bindings LIKE '%}}' AND bindings NOT LIKE '%{Key}%';";
            }
        }

        public static IEnumerable<string> Down()
        {
            foreach (var missionId in Missions)
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Quote(Suffix(missionId))}, '}}') " +
                    $"WHERE mission_id = {missionId} AND content_revision = '{D.Revision}' AND bindings LIKE {Quote("%" + Suffix(missionId))};";

            yield return $"DELETE FROM mission_indicator WHERE mission_id = {D.SeverTheHead} " +
                $"AND content_revision = '{D.Revision}' AND objective_id = {NodeObjective} AND indicator_id = {NodeCMarker};";
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Quote(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
