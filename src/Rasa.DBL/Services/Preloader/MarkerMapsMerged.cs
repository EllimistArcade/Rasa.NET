using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Rasa.Missions.Content;

namespace Rasa.Services.Preloader
{
    /// <summary>
    /// The scenes three migrations give marker maps to, made the same whichever ran first
    /// (Marker_maps_merged).
    ///
    /// Mission_marker_maps (20271103000000), Devils_den_markers (20271112000000) and
    /// Instance_marker_maps (20271113000000) each add a scene's indicatorMaps only where it has none.
    /// Built on their own, each mapped some markers the others did not:
    ///
    /// - A world made from scratch runs Mission_marker_maps first, so the Den's Elohim Artifacts (381)
    ///   and The Missing Strike Team (670) keep objective 1's map only (Kogari's and Brody's markers at
    ///   Hightower are left to the default), and Sever the Devil's Head (672) has no map for the third
    ///   power node's marker, which Devils_den_markers adds: it is sent on the Palisades.
    /// - A world that had Devils_den_markers and Instance_marker_maps before Mission_marker_maps came
    ///   keeps their maps, so Looking For Logos? (1308) has no map for the Far Logos shrine's marker in
    ///   Turpis Refinery that Mission_marker_maps puts in.
    ///
    /// Each such scene gets every map both name, as the side whose maps take in the other's writes
    /// them. Up replaces the one form with the other where the scene has it; again, it finds nothing.
    /// Down gives the form a world made from scratch had (Mission_marker_maps'); the other form
    /// (1308's in a world that had the two first) is left as the union, which names only markers that
    /// are there until Mission_marker_maps' own Down takes the shrine's off with its map.
    /// </summary>
    public static class MarkerMapsMerged
    {
        public const string Key = MissionMarkerMaps.Key;

        /// <summary>A scene's maps as each migration writes them: (mission, revision, objective → indicator → map, the text).</summary>
        private sealed record Form(uint MissionId, string Revision, Dictionary<uint, Dictionary<uint, uint>> Maps, string Text, bool FirstInANewWorld);

        private static IEnumerable<Form> Forms()
        {
            foreach (var (missionId, revision) in MissionMarkerMaps.Scenes())
                yield return new Form(missionId, revision, Maps(MissionMarkerMaps.Markers.Where(m => m.MissionId == missionId)
                    .Select(m => (m.ObjectiveId, m.IndicatorId, m.MapContextId))), MissionMarkerMaps.Property(missionId), true);

            foreach (var missionId in DevilsDenMarkers.Markers.Select(m => m.MissionId).Distinct())
                yield return new Form(missionId, DevilsDenContent.Revision, Maps(DevilsDenMarkers.Markers.Where(m => m.MissionId == missionId)
                    .Select(m => (m.Objective, m.Indicator, m.Map))), DevilsDenMarkers.Key + DevilsDenMarkers.MapsJson(missionId), false);

            foreach (var group in InstanceMarkerMaps.Markers.Where(m => m.MissionId != CraterLakeContent.ReclaimingWhatsOurs)
                         .GroupBy(m => (m.MissionId, m.Revision)))
                yield return new Form(group.Key.MissionId, group.Key.Revision, Maps(group.Select(m => (m.Objective, m.Indicator, m.Map))),
                    InstanceMarkerMaps.Key + InstanceMarkerMaps.MapsJson(group.Key.MissionId), false);
        }

        private static Dictionary<uint, Dictionary<uint, uint>> Maps(IEnumerable<(uint Objective, uint Indicator, uint Map)> markers) =>
            markers.GroupBy(m => m.Objective).ToDictionary(g => g.Key, g => g.ToDictionary(m => m.Indicator, m => m.Map));

        private static bool Within(Dictionary<uint, Dictionary<uint, uint>> part, Dictionary<uint, Dictionary<uint, uint>> whole) =>
            part.All(o => whole.TryGetValue(o.Key, out var indicators) &&
                o.Value.All(i => indicators.TryGetValue(i.Key, out var map) && map == i.Value));

        /// <summary>
        /// Each scene two migrations map differently: the form whose maps take in the other's, and the one it replaces.
        /// </summary>
        public static IEnumerable<(uint MissionId, string Revision, string From, string To, bool FromFirstInANewWorld)> Replacements()
        {
            foreach (var scene in Forms().GroupBy(f => (f.MissionId, f.Revision)).Where(g => g.Count() == 2))
            {
                var (a, b) = (scene.First(), scene.Last());

                if (Within(a.Maps, b.Maps) && Within(b.Maps, a.Maps))
                    continue;   // the same maps, whatever the text

                if (Within(a.Maps, b.Maps))
                    yield return (scene.Key.MissionId, scene.Key.Revision, a.Text, b.Text, a.FirstInANewWorld);
                else if (Within(b.Maps, a.Maps))
                    yield return (scene.Key.MissionId, scene.Key.Revision, b.Text, a.Text, b.FirstInANewWorld);
                else
                {
                    var union = a.Maps.ToDictionary(o => o.Key, o => new Dictionary<uint, uint>(o.Value));
                    foreach (var (objective, indicators) in b.Maps)
                        foreach (var (indicator, map) in indicators)
                            (union.TryGetValue(objective, out var into) ? into : union[objective] = new())[indicator] = map;
                    var text = Key + JsonSerializer.Serialize(union, MissionContentCodec.Options);
                    yield return (scene.Key.MissionId, scene.Key.Revision, a.Text, text, a.FirstInANewWorld);
                    yield return (scene.Key.MissionId, scene.Key.Revision, b.Text, text, b.FirstInANewWorld);
                }
            }
        }

        public static IEnumerable<string> Up()
        {
            foreach (var (missionId, revision, from, to, _) in Replacements())
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(from)}, {Q(to)}) " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} AND bindings LIKE {Q("%" + from + "%")}" +
                    $"{Indicators(missionId, revision, to)};";
        }

        public static IEnumerable<string> Down()
        {
            foreach (var (missionId, revision, from, to, first) in Replacements().Where(r => r.FromFirstInANewWorld))
                yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(to)}, {Q(from)}) " +
                    $"WHERE mission_id = {missionId} AND content_revision = {Q(revision)} AND bindings LIKE {Q("%" + to + "%")};";
        }

        /// <summary>Only where every marker the maps name is there: a scene's indicatorMaps may name only its mission's markers.</summary>
        private static string Indicators(uint missionId, string revision, string text)
        {
            var maps = JsonSerializer.Deserialize<Dictionary<uint, Dictionary<uint, uint>>>(text.Substring(Key.Length));
            return string.Concat(maps.SelectMany(o => o.Value.Keys.Select(indicator =>
                $" AND EXISTS (SELECT 1 FROM mission_indicator i WHERE i.mission_id = {missionId} AND i.content_revision = {Q(revision)} " +
                $"AND i.objective_id = {o.Key} AND i.indicator_id = {indicator})")));
        }

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
