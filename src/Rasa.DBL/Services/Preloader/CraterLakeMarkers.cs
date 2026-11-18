using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Rasa.Data;
using Rasa.Missions.Content;
using Rasa.Missions.Definitions;
using Rasa.Missions.Runtime;
using Rasa.Missions.Scenes;
using Rasa.Services.Preloader.Missions;
using Rasa.Structures;
using Rasa.Structures.World;

namespace Rasa.Services.Preloader
{
    using C = CraterLakeContent;

    /// <summary>
    /// Crater Lake Research Facility's mission markers where they were wrong or missing
    /// (Crater_lake_markers). A marker is sent only on its map (MissionMarkerProjection); these
    /// showed on the wrong one, at the wrong time, or not where the work is.
    ///
    /// - Ghost in the Machine (1059), V2. Objective 2, "Return Casper's dogtags to Franja Corman in
    ///   Ranja Gorge", was the dogtags held (an item objective, which a drop has to name), open from
    ///   the start: its marker, the facility's door, showed beside Casper's before he was dead, and
    ///   went when the dogtags were looted - just when the player had to go to Franja - and nothing
    ///   pointed at her on the Wilderness. The client's hidden objective 3 (8183/8184) is now the
    ///   dogtags held, and the drop's; objective 2 opens with them and is not required, so it stays
    ///   open, with its markers, until Franja takes the mission: the facility's door in the
    ///   facility (1721) and Franja at Ranja Gorge on the Wilderness (1220). A character on V1's
    ///   assignment has it cleared at the next login (its objectives are not V2's) and is offered
    ///   it again on coming into the facility; dogtags still held count at once.
    /// - Destroying the Evidence (1055) objective 2: the Biological Lab's terminal, and its marker,
    ///   stood in the greenhouse on a patch of floor no route reaches (215 m2 of navmesh of its
    ///   own); they are outside its north wall (CraterLakeContent.BiologicalTerminal), where the
    ///   terminal is put down now. The fourth storage crate likewise (CraterLakeContent.Crates).
    /// - The Dead Live (450) objective 4, speak with Velns: its one marker is the Wilderness' door
    ///   to the facility (Marker_map_scopes). A second, sent in the facility, is on Velns.
    /// - Centrifuge Fuel (489): its one marker, on the Wilderness' door, is behind the player in
    ///   the facility, where the crates are. One on each destroyed building with crates: the
    ///   research HQ, the greenhouses, the Corman HQ and the housing on the ridge, sent in the facility.
    ///
    /// Plain SQL for both providers. Guarded: a marker that is not where this found it is left as
    /// it is, a scene's markers' maps are changed only from the exact form this found, and Down
    /// takes out exactly what Up put in and writes V1 of 1059 again.
    /// </summary>
    public static class CraterLakeMarkers
    {
        public const string Revision = C.Revision;

        /// <summary>Ghost in the Machine's client-hidden objective: the dogtags held.</summary>
        public const uint DogtagsHeld = 3;

        public const uint DoorMarker = MissionIndicator.UnnamedFrom + C.ReturnDogtags;
        public const uint FranjaMarker = MissionIndicator.UnnamedFrom + 1000 + C.ReturnDogtags;
        public const uint VelnsMarker = MissionIndicator.UnnamedFrom + 1000 + C.SpeakToVelns;

        /// <summary>Centrifuge Fuel's markers in the facility: one a building with crates (CraterLakeContent.Crates).</summary>
        public static readonly (uint Id, Vector3 At, string Where)[] CrateMarkers =
        {
            (MissionIndicator.UnnamedFrom + 1000 + C.FindCrystals, C.Crates[0], "the research HQ"),
            (MissionIndicator.UnnamedFrom + 2000 + C.FindCrystals, C.Crates[2], "the greenhouses"),
            (MissionIndicator.UnnamedFrom + 3000 + C.FindCrystals, C.Crates[4], "the Corman HQ"),
            (MissionIndicator.UnnamedFrom + 4000 + C.FindCrystals, C.Crates[6], "the housing on the ridge")
        };

        public const double CrateMarkerRadius = 15;

        /// <summary>The Biological Lab's marker as it was: the walkthrough's place, in the greenhouse.</summary>
        public const double WasX = 102.3, WasY = 118.94, WasZ = -89.6;

        public static double NowX => C.BiologicalTerminal.X;
        public static double NowY => C.BiologicalTerminal.Y;
        public static double NowZ => C.BiologicalTerminal.Z;

        /// <summary>The Dead Live's objective 4 entry as Marker_map_scopes left it, and with Velns's marker.</summary>
        public static readonly string DeadLiveEntry = "\"4\":{\"1000000004\":1220}",
            DeadLiveEntryWithVelns = "\"4\":{\"1000000004\":1220,\"1000001004\":1721}";

        /// <summary>Centrifuge Fuel's indicatorMaps, appended as the scene's last property.</summary>
        public static string CentrifugeMaps =>
            MarkerMapScopes.Key + JsonSerializer(new Dictionary<uint, Dictionary<uint, uint>>
            {
                [C.FindCrystals] = CrateMarkers.ToDictionary(m => m.Id, _ => C.CraterLake)
            });

        private static string JsonSerializer(object value) => System.Text.Json.JsonSerializer.Serialize(value, MissionContentCodec.Options);

        public static IEnumerable<string> Up(bool mySql)
        {
            // Ghost in the Machine: V1 out, V2 in.
            foreach (var sql in InstanceMissionSql.Remove(Revision, C.GhostInTheMachine))
                yield return sql;
            foreach (var sql in GhostInTheMachineV2())
                yield return sql;

            // The Biological Lab's marker, outside the greenhouse.
            yield return $"UPDATE mission_indicator SET pos_x = {N(NowX)}, pos_y = {N(NowY)}, pos_z = {N(NowZ)} " +
                $"WHERE {Key(C.DestroyingTheEvidence, C.BiologicalLab, MissionIndicator.UnnamedFrom + C.BiologicalLab)} " +
                $"AND ABS(pos_x - {N(WasX)}) < 0.01 AND ABS(pos_z - {N(WasZ)}) < 0.01;";

            // The Dead Live: Velns, in the facility.
            yield return Insert(C.TheDeadLive, C.SpeakToVelns, VelnsMarker, C.VelnsSpot, 8);
            yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(DeadLiveEntry)}, {Q(DeadLiveEntryWithVelns)}) " +
                $"WHERE mission_id = {C.TheDeadLive} AND content_revision = {Q(Revision)} AND bindings LIKE {Q("%" + DeadLiveEntry + "%")};";

            // Centrifuge Fuel: the buildings with crates, in the facility.
            foreach (var (id, at, _) in CrateMarkers)
                yield return Insert(C.CentrifugeFuel, C.FindCrystals, id, at, CrateMarkerRadius);
            var suffix = $",{CentrifugeMaps}}}";
            var head = "SUBSTR(bindings, 1, LENGTH(bindings) - 1)";
            var joined = mySql ? $"CONCAT({head}, {Q(suffix)})" : $"{head} || {Q(suffix)}";
            yield return $"UPDATE mission_scene_binding SET bindings = {joined} " +
                $"WHERE mission_id = {C.CentrifugeFuel} AND content_revision = {Q(Revision)} " +
                $"AND bindings LIKE '%}}' AND bindings NOT LIKE '%{MarkerMapScopes.Key}%';";
        }

        public static IEnumerable<string> Down()
        {
            var suffix = $",{CentrifugeMaps}}}";
            yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(suffix)}, '}}') " +
                $"WHERE mission_id = {C.CentrifugeFuel} AND content_revision = {Q(Revision)} AND bindings LIKE {Q("%" + suffix)};";
            foreach (var (id, at, _) in CrateMarkers)
                yield return Delete(C.CentrifugeFuel, C.FindCrystals, id, at);

            yield return $"UPDATE mission_scene_binding SET bindings = REPLACE(bindings, {Q(DeadLiveEntryWithVelns)}, {Q(DeadLiveEntry)}) " +
                $"WHERE mission_id = {C.TheDeadLive} AND content_revision = {Q(Revision)} AND bindings LIKE {Q("%" + DeadLiveEntryWithVelns + "%")};";
            yield return Delete(C.TheDeadLive, C.SpeakToVelns, VelnsMarker, C.VelnsSpot);

            yield return $"UPDATE mission_indicator SET pos_x = {N(WasX)}, pos_y = {N(WasY)}, pos_z = {N(WasZ)} " +
                $"WHERE {Key(C.DestroyingTheEvidence, C.BiologicalLab, MissionIndicator.UnnamedFrom + C.BiologicalLab)} " +
                $"AND ABS(pos_x - {N(NowX)}) < 0.01 AND ABS(pos_z - {N(NowZ)}) < 0.01;";

            foreach (var sql in InstanceMissionSql.Remove(Revision, C.GhostInTheMachine))
                yield return sql;
            var v1 = new List<string>();
            C.GhostInTheMachineRows(v1);
            foreach (var sql in v1)
                yield return sql;
        }

        /// <summary>
        /// HQ's, by radio after We Be Jammin': Casper dead, his dogtags held (the client's hidden
        /// objective), and objective 2 open with its markers until Franja takes them.
        /// </summary>
        public static IEnumerable<string> GhostInTheMachineV2()
        {
            var sql = new List<string>();
            var mission = new InstanceMissionSql(sql, Revision, C.GhostInTheMachine, "Ghost in the Machine", 5944, null, C.Franja, 10, C.Category);
            mission.Reward(8000, 1500);
            mission.Prerequisite(C.WeBeJammin);
            mission.Objective(C.EngageCasper, 5981, 7391, 1, MissionObjectiveState.Incomplete);
            mission.Objective(C.ReturnDogtags, 6004, 7392, 2, required: false);
            // Open from the start: the dogtags drop to whoever has it open when Casper dies.
            mission.Objective(DogtagsHeld, 8183, 8184, 3, MissionObjectiveState.Incomplete);
            mission.Transition(C.EngageCasper);
            mission.Progress(C.EngageCasper, MissionProgressEventKind.CreatureKilled, C.Casper);
            mission.Transition(DogtagsHeld);
            mission.Progress(DogtagsHeld, MissionProgressEventKind.ItemAcquired, C.DogtagsClass, target: 1);
            mission.Action(DogtagsHeld, 1, MissionActionKind.GrantReward, reward: 1);
            mission.RevealAndActivate(DogtagsHeld, C.ReturnDogtags, 2);
            mission.Indicator(C.EngageCasper, MissionIndicator.UnnamedFrom + C.EngageCasper, C.CasperSpot.X, C.CasperSpot.Y, C.CasperSpot.Z, 15);
            mission.Indicator(C.ReturnDogtags, DoorMarker, C.Arrival.X, C.Arrival.Y, C.Arrival.Z);
            mission.Indicator(C.ReturnDogtags, FranjaMarker, C.FranjaAtRanja.X, C.FranjaAtRanja.Y, C.FranjaAtRanja.Z);
            mission.Channels(MissionChannel.Radio, MissionChannel.Npc,
                MissionOfferSourceDefinition.MissionRewarded(C.WeBeJammin),
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, MissionOfferSourceDefinition.MapArrivalKey, C.CraterLake));
            C.Evidence(mission, "HQ's scan after We Be Jammin'1054 (its opening is HQ's): offered by radio at its turn-in, and again on " +
                "coming into the facility. Hominis Machina Lt. Casper520012 on the fortification's platform (BossSpawnpoolPreloader) " +
                "dead is objective1, and drops Casper's Dogtags625 to whoever has the client's hidden objective3 open; the dogtags " +
                "held are it, and open objective2, 'Return Casper's dogtags', which is not required and stays open, its markers " +
                "with it, until Dr. Franja Corman107 (her finishing text) takes the dogtags with the mission: the facility's " +
                "door in the facility, Franja at Ranja Gorge on the Wilderness (Crater_lake_markers, V2).");
            var scene = C.Scene();
            scene.HiddenObjectiveIds = new List<uint> { DogtagsHeld };
            scene.ExistingFactObjectiveIds = new List<uint> { DogtagsHeld };
            scene.IndicatorMaps = new Dictionary<uint, Dictionary<uint, uint>>
            {
                [C.ReturnDogtags] = new() { [DoorMarker] = C.CraterLake, [FranjaMarker] = C.Wilderness }
            };
            scene.Items = new List<MissionItemBinding>
            {
                new("dogtags", C.DogtagsTemplate, MissionItemScope.CharacterOwned, 1,
                    MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain,
                    TurnInQuantity: 1, Drop: new MissionItemDropDefinition(new[] { C.Casper }, DogtagsHeld, C.CraterLake, 100, 1))
            };
            mission.Enable(scene);
            return sql;
        }

        private static string Insert(uint missionId, uint objectiveId, uint indicatorId, Vector3 at, double radius) =>
            "INSERT INTO mission_indicator (mission_id, content_revision, requirement, objective_id, indicator_id, pos_x, pos_y, pos_z, radius, show_3d_effect, comment) " +
            $"SELECT d.mission_id, d.content_revision, {(byte)MissionContentRequirement.Required}, {objectiveId}, {indicatorId}, " +
            $"{N(at.X)}, {N(at.Y)}, {N(at.Z)}, {N(radius)}, 0, 'Objective marker' " +
            $"FROM mission_content_definition d WHERE d.mission_id = {missionId} AND d.content_revision = {Q(Revision)} " +
            $"AND NOT EXISTS (SELECT 1 FROM mission_indicator x WHERE {Key(missionId, objectiveId, indicatorId, "x")});";

        private static string Delete(uint missionId, uint objectiveId, uint indicatorId, Vector3 at) =>
            $"DELETE FROM mission_indicator WHERE {Key(missionId, objectiveId, indicatorId)} " +
            $"AND ABS(pos_x - {N(at.X)}) < 0.01 AND ABS(pos_z - {N(at.Z)}) < 0.01;";

        private static string Key(uint missionId, uint objectiveId, uint indicatorId, string table = null)
        {
            var t = table == null ? "" : table + ".";
            return $"{t}mission_id = {missionId} AND {t}content_revision = {Q(Revision)} AND {t}objective_id = {objectiveId} AND {t}indicator_id = {indicatorId}";
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string N(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";
    }
}
