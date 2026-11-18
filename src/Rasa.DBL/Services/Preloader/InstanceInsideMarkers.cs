using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Rasa.Data;
using Rasa.Missions.Content;
using Rasa.Missions.Definitions;
using Rasa.Structures;
using Rasa.Structures.World;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// Markers inside the instance for objectives marked only at its entrance (Instance_inside_markers).
    /// A marker is sent only on its map (MissionMarkerProjection), so a player who went through
    /// the entrance had nothing on the map for the work in there. Each gets a marker, sent in the
    /// instance, where the work is; the entrance's stays as it is.
    ///
    /// - Retrieval for Recon (382) objective 1, find Tyler: on Tyler, in the caverns' southern vault (Minos Caverns, 1347).
    /// - Destroying the Warnet Barrier (384) objective 1, the nests: on the three groups of them
    ///   (MinosCavernsContent.Nests) - the upper tunnel's two, the main cavern's five, the vault's two.
    /// - The Escapist (593) objective 1, speak with Nylla: on Nylla at the encampment by the way in (Pravus, 1430).
    /// - Fried Atta (1491) objective 1, give John the canister: on John at the camp (Ojasa Atta Hive, 1865).
    /// - Scarecrow (1793) objective 1, five Miasma essences: on the Miasma of the second chamber and
    ///   of the lower passage (WarnetCavernsContent's pools 571031 and 571036; Warnet Caverns, 1384).
    /// - Trophy Case (1794) objective 2, fifteen caverns trophies: on the warnets of the first
    ///   chamber and of the Research Base Camp (pools 571012 and 571020).
    ///
    /// Plain SQL for both providers. Guarded: a marker is put in only where there is none of its
    /// id, a scene's maps are added to only in the exact form this found, and Down takes out
    /// exactly what Up put in.
    /// </summary>
    public static class InstanceInsideMarkers
    {
        public sealed record Marker(uint MissionId, string Revision, uint Objective, uint Indicator, Vector3 At, double Radius, uint Map, string Where);

        public const uint MinosCaverns = MinosCavernsContent.Map, Pravus = PravusResearchContent.Pravus, Hive = OjasaHiveContent.Hive,
            WarnetCaverns = WarnetCavernsContent.Map;

        private static uint Id(uint k, uint objective) => MissionIndicator.UnnamedFrom + 1000 * k + objective;

        public static readonly Marker[] Markers =
        {
            new(MinosCavernsMissions.RetrievalForRecon, MinosCavernsMissions.Revision, 1, Id(1, 1), MinosCavernsMissions.TylerSpot, 10,
                MinosCaverns, "Tyler in the southern vault"),
            new(MinosCavernsMissions.WarnetBarrier, MinosCavernsMissions.Revision, 1, Id(1, 1), new Vector3(11f, 30.21f, 8f), 22,
                MinosCaverns, "the upper tunnel's two nests"),
            new(MinosCavernsMissions.WarnetBarrier, MinosCavernsMissions.Revision, 1, Id(2, 1), new Vector3(67.74f, 15.67f, 43.71f), 36,
                MinosCaverns, "the main cavern's five nests"),
            new(MinosCavernsMissions.WarnetBarrier, MinosCavernsMissions.Revision, 1, Id(3, 1), new Vector3(49.94f, -12.93f, -37.89f), 28,
                MinosCaverns, "the vault's two nests"),
            new(PravusResearchContent.Escapist, PravusResearchContent.Revision, 1, Id(1, 1), PravusResearchContent.NyllaAtCamp, 8,
                Pravus, "Nylla at the encampment"),
            new(OjasaHiveContent.FriedAtta, OjasaHiveContent.Revision, 1, Id(1, 1), OjasaHiveContent.JohnAtCamp, 8,
                Hive, "John at the camp"),
            new(WarnetCavernsMissions.Scarecrow, WarnetCavernsMissions.Revision, 1, Id(1, 1), new Vector3(120.4f, 61.75f, 131.6f), 20,
                WarnetCaverns, "the second chamber's Miasma"),
            new(WarnetCavernsMissions.Scarecrow, WarnetCavernsMissions.Revision, 1, Id(2, 1), new Vector3(211.6f, 43.75f, 113.4f), 15,
                WarnetCaverns, "the lower passage's Miasma"),
            new(WarnetCavernsMissions.TrophyCase, WarnetCavernsMissions.Revision, 2, Id(1, 2), new Vector3(12.4f, 73.55f, 70.6f), 25,
                WarnetCaverns, "the first chamber's warnets"),
            new(WarnetCavernsMissions.TrophyCase, WarnetCavernsMissions.Revision, 2, Id(2, 2), new Vector3(64.6f, 83.95f, -1.8f), 40,
                WarnetCaverns, "the Research Base Camp's warnets")
        };

        /// <summary>The scenes with no indicatorMaps: these get theirs, as the scene's last property.</summary>
        public static readonly uint[] AppendedScenes =
            { MinosCavernsMissions.RetrievalForRecon, MinosCavernsMissions.WarnetBarrier, PravusResearchContent.Escapist, OjasaHiveContent.FriedAtta };

        /// <summary>The Warnet Caverns scenes' objective entry, the entrance's alone, and with the markers inside.</summary>
        public static IEnumerable<(uint MissionId, string Was, string Now)> Entries() =>
            new[] { WarnetCavernsMissions.Scarecrow, WarnetCavernsMissions.TrophyCase }.Select(missionId =>
            {
                var objective = Markers.First(m => m.MissionId == missionId).Objective;
                var was = $"\"{objective}\":{{\"{WarnetCavernsMissions.EntranceMarker}\":{WarnetCavernsContent.Palisades}";
                var inside = string.Concat(Markers.Where(m => m.MissionId == missionId).Select(m => $",\"{m.Indicator}\":{m.Map}"));
                return (missionId, was + "}", was + inside + "}");
            });

        /// <summary>A scene's indicatorMaps for its markers here, as MissionContentCodec writes them.</summary>
        public static string Maps(uint missionId) =>
            MarkerMapScopes.Key + System.Text.Json.JsonSerializer.Serialize(Markers.Where(m => m.MissionId == missionId)
                .GroupBy(m => m.Objective)
                .ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Indicator, m => m.Map)), MissionContentCodec.Options);

        public static IEnumerable<string> Up(bool mySql)
        {
            foreach (var m in Markers)
                yield return Insert(m);

            foreach (var missionId in AppendedScenes)
            {
                var revision = Markers.First(m => m.MissionId == missionId).Revision;
                var suffix = $",{Maps(missionId)}}}";
                var head = "SUBSTR(bindings, 1, LENGTH(bindings) - 1)";
                var joined = mySql ? $"CONCAT({head}, {Q(suffix)})" : $"{head} || {Q(suffix)}";
                yield return $"UPDATE mission_scene_binding SET bindings = {joined} " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} " +
                    $"AND bindings LIKE '%}}' AND bindings NOT LIKE '%{MarkerMapScopes.Key}%';";
            }

            foreach (var (missionId, was, now) in Entries())
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(was)}, {Q(now)}) " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(WarnetCavernsMissions.Revision)} " +
                    $"AND bindings LIKE {Q("%" + was + "%")};";
        }

        public static IEnumerable<string> Down()
        {
            foreach (var (missionId, was, now) in Entries())
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(now)}, {Q(was)}) " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(WarnetCavernsMissions.Revision)} " +
                    $"AND bindings LIKE {Q("%" + now + "%")};";

            foreach (var missionId in AppendedScenes)
            {
                var revision = Markers.First(m => m.MissionId == missionId).Revision;
                var suffix = $",{Maps(missionId)}}}";
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(suffix)}, '}}') " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} AND bindings LIKE {Q("%" + suffix)};";
            }

            foreach (var m in Markers)
                yield return $"DELETE FROM mission_indicator WHERE {Key(m)} " +
                    $"AND ABS(pos_x - {N(m.At.X)}) < 0.01 AND ABS(pos_z - {N(m.At.Z)}) < 0.01;";
        }

        private static string Insert(Marker m) =>
            "INSERT INTO mission_indicator (mission_id, content_revision, requirement, objective_id, indicator_id, pos_x, pos_y, pos_z, radius, show_3d_effect, comment) " +
            $"SELECT d.mission_id, d.content_revision, {(byte)MissionContentRequirement.Required}, {m.Objective}, {m.Indicator}, " +
            $"{N(m.At.X)}, {N(m.At.Y)}, {N(m.At.Z)}, {N(m.Radius)}, 0, 'Objective marker' " +
            $"FROM mission_content_definition d WHERE d.mission_id = {m.MissionId} AND d.content_revision = {Q(m.Revision)} " +
            $"AND NOT EXISTS (SELECT 1 FROM mission_indicator x WHERE {Key(m, "x")});";

        private static string Key(Marker m, string table = null)
        {
            var t = table == null ? "" : table + ".";
            return $"{t}mission_id = {m.MissionId} AND {t}content_revision = {Q(m.Revision)} AND {t}objective_id = {m.Objective} AND {t}indicator_id = {m.Indicator}";
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string N(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
