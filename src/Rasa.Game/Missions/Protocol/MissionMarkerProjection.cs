using System;
using System.Collections.Generic;
using System.Linq;
using Rasa.Missions.Content;
using Rasa.Missions.Scenes;
using Rasa.Missions.Runtime;
using Rasa.Structures;
using Rasa.Structures.World;

namespace Rasa.Managers
{
    /// <summary>Map scope is server metadata: retail indicator tuples contain only position/radius/name/3D flag.</summary>
    internal static class MissionMarkerProjection
    {
        internal static void LoadDefaults(MissionContentCatalog catalog, IEnumerable<SpawnPoolEntry> pools)
        {
            var poolMaps = pools.ToDictionary(p => p.Id, p => p.MapContextId);
            var creatureMaps = pools.SelectMany(p => new[] { p.Creature1Id, p.Creature2Id, p.Creature3Id,
                    p.Creature4Id, p.Creature5Id, p.Creature6Id }.Where(id => id != 0).Select(id => (Id: id, Map: p.MapContextId)))
                .ToLookup(p => p.Id, p => p.Map);
            foreach (var mission in catalog.Missions.Values.Where(m => m.Objectives.Values.Any(o => o.Indicators.Count > 0)))
            {
                catalog.SceneBindings.TryGetValue(mission.MissionId, out var scene);
                if (scene?.MapContextId > 0) { catalog.IndicatorMapDefaults[mission.MissionId] = scene.MapContextId; continue; }
                var maps = new HashSet<uint>();
                if (catalog.Areas.TryGetValue(mission.MissionId, out var areas))
                    maps.UnionWith(areas.Values.Select(a => a.MapContextId));
                if (catalog.SpawnGroups.TryGetValue(mission.MissionId, out var groups))
                    maps.UnionWith(groups.Values.Select(g => g.MapContextId));
                maps.UnionWith(mission.Items.Values.Where(i => i.Drop != null).Select(i => i.Drop.MapContextId));
                maps.UnionWith(catalog.Experiences.Where(e => e.MissionTriggers.Any(t => t.MissionId == mission.MissionId))
                    .Select(e => e.MapContextId));
                if (scene != null)
                    foreach (var actor in scene.Actors.Values.Where(a => a.Kind == SceneActorKind.PublicSpawn))
                        if (poolMaps.TryGetValue(actor.TemplateId, out var map)) maps.Add(map);
                maps.Remove(0);
                // Simple NPC-only missions may have no scene/area. Use their placed contacts
                // only when no authored world scope is available and all contacts agree.
                if (maps.Count == 0)
                {
                    if (mission.MissionGiver.HasValue) maps.UnionWith(creatureMaps[mission.MissionGiver.Value]);
                    if (mission.MissionReciver.HasValue) maps.UnionWith(creatureMaps[mission.MissionReciver.Value]);
                    maps.Remove(0);
                }
                catalog.IndicatorMapDefaults[mission.MissionId] = maps.Count == 1 ? maps.Single() : 0;
            }
        }

        internal static void Validate(Mission mission, MissionSceneDefinition scene)
        {
            if (scene.IndicatorMaps == null) return;
            foreach (var objective in scene.IndicatorMaps)
            {
                if (!mission.Objectives.TryGetValue(objective.Key, out var definition) || objective.Value == null)
                    throw new MissionRuleException($"Mission {mission.MissionId}: indicator maps name an unknown objective.");
                foreach (var entry in objective.Value)
                    if (entry.Value == 0 || !definition.Indicators.Any(i => i.IndicatorId == entry.Key))
                        throw new MissionRuleException($"Mission {mission.MissionId}: indicator maps require an existing indicator and a nonzero map id.");
            }
        }

        internal static uint ResolveMap(MissionContentCatalog catalog, uint missionId, uint objectiveId, MissionIndicator indicator)
        {
            if (indicator.MapContextId != 0) return indicator.MapContextId;
            if (catalog.SceneBindings.TryGetValue(missionId, out var scene) && scene.IndicatorMaps != null &&
                scene.IndicatorMaps.TryGetValue(objectiveId, out var overrides) &&
                overrides.TryGetValue(indicator.IndicatorId, out var authoredMap)) return authoredMap;

            // Existing database rows predate map metadata. These cross-map quests cannot use
            // a single mission-wide default. Match their authored markers, never coordinates.
            // Sources: TorcastraMissionsV1, DevilsDenContent, MinosCavernsMissions,
            // PravusResearchContent and OjasaHiveContent in Services/Preloader.
            var legacyMap = (missionId, objectiveId, indicator.IndicatorId) switch
            {
                (356, 4 or 5 or 8 or 9 or 10 or 11, var id) when id == MissionIndicator.UnnamedFrom + objectiveId => 1349U,
                (397, 2, 24) => 1349U, // Prison cell switch
                (399, >= 1 and <= 5, 10000001) => 1349U, // Five prison cell blocks
                (380, 1, 1000000001) => 1394U, // Caritas in the Den
                (671, 3, 1000000003) or (671, 8, 1000000008) => 1394U,
                (671, 7, 1000000007) => 1244U, // Return to Cyrida in Palisades
                (382 or 384, 1, 33) => 1148U, // Minos cave mouth on the Divide
                (593, 1, 54) => 1220U, // Pravus entrance in Wilderness
                (1491, 1, 278) => 1761U, // Hive entrance on Incline
                (1491, 2, 1000000002) => 1865U, // John inside the hive
                _ => 0U
            };
            return legacyMap != 0 ? legacyMap : catalog.IndicatorMapDefaults.GetValueOrDefault(missionId);
        }

        internal static MissionInfo Apply(Manifestation player, uint missionId, MissionInfo info, MissionContentCatalog catalog)
        {
            var currentMap = player?.MapContextId ?? 0;
            foreach (var objective in info.ObjectivesList)
            {
                var visible = new List<MissionIndicator>();
                foreach (var indicator in objective.IndicatorList)
                {
                    var map = ResolveMap(catalog, missionId, objective.ObjectiveId, indicator);
                    // Missing/ambiguous scope is not a marker that should appear on every map.
                    if (map == 0 || map != currentMap) continue;
                    visible.Add(new MissionIndicator { MapContextId = map, Position = indicator.Position,
                        Radius = indicator.Radius, IndicatorId = indicator.IndicatorId, Show3DEffect = indicator.Show3DEffect });
                }
                objective.IndicatorList.Clear();
                objective.IndicatorList.AddRange(visible);
            }
            return info;
        }
    }
}
