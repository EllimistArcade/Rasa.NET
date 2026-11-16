using System.Collections.Generic;
using System.Linq;
using Rasa.Data;
using Rasa.Missions.Content;
using Rasa.Missions.Definitions;
using Rasa.Missions.Runtime;
using Rasa.Missions.Scenes;
using Rasa.Structures.World;

namespace Rasa.Services.Preloader.Missions.Purgas
{
    using Station = Rasa.Services.Preloader.PurgasStationContent;
    using Depths = Rasa.Services.Preloader.PurgasStationDepthsContent;

    /// <summary>
    /// Purgas Station's lower missions (Purgas_station_lower_levels), on what PurgasStationDepthsContent
    /// puts down and the Game's PurgasStation runs, under PurgasStationMissionsV1's revision:
    ///  - 850 A Means to an End (HQ by radio once Off Grid is done; radioed in): the seven fuel basins,
    ///    then the Level 3 Access Keypass.
    ///  - 852 Reactor Overload (HQ by radio once A Means to an End is done; radioed in): the reactor's
    ///    four fuel pumps, the Level 3 Access Keypass, then the stabilization module overdriven.
    ///
    /// The client gives the missions, objectives, texts and counters ("Fuel Basins in Production",
    /// "Reactor Fuel Pumps Remaining"); the walkthrough the order of things, the credits and what kind of
    /// item each gives. Ours: the keypass taken after the basins and before the module (the walkthrough
    /// does not say where it is); the experience at ten to a credit; the items, level 40-44 Uncommon.
    /// </summary>
    public static class PurgasStationDepthsMissionsV1
    {
        public const uint MeansToAnEnd = 850, ReactorOverload = 852;

        public static readonly uint[] All = { MeansToAnEnd, ReactorOverload };

        public const string Revision = PurgasStationMissionsV1.Revision;

        public const string MeansToAnEndOfferKey = "purgas.means-to-an-end", ReactorOverloadOfferKey = "purgas.reactor-overload";

        /// <summary>The Level 3 Access Keypass: MisDivideItemPurgasLevel3Pass.</summary>
        public const uint KeypassClass = 20743, KeypassTemplate = 42228;

        public static class Events
        {
            // 850: scenario 1, basin n shut down is event n.
            public const uint Basins = 1;

            // 852: scenario 1, pump n down is event n; scenario 2, the module overdriven.
            public const uint Pumps = 1, Module = 2;
            public const uint Overdriven = 1;
        }

        /// <summary>A marker id of no client name: one per place, the client naming each by its objective.</summary>
        public static uint Unnamed(uint objective, uint place = 0) => Rasa.Structures.MissionIndicator.UnnamedFrom + place * 1000 + objective;

        public static IEnumerable<string> Up()
        {
            var sql = new List<string>();

            MeansToAnEndRows(sql);
            ReactorOverloadRows(sql);

            return sql;
        }

        public static IEnumerable<string> Down() => InstanceMissionSql.Remove(Revision, All);

        private static InstanceMissionSql.RewardItem[] Choice(params uint[] templates) =>
            templates.Select(template => new InstanceMissionSql.RewardItem(template, 1)).ToArray();

        private static uint[] Upto(uint count) => Enumerable.Range(1, (int)count).Select(n => (uint)n).ToArray();

        private static void OnEvents(InstanceMissionSql mission, uint objective, uint scenario, uint count)
        {
            for (var eventId = 1U; eventId <= count; eventId++)
                mission.Progress(objective, MissionProgressEventKind.ScenarioEvent, eventId, counter: scenario, target: count, trigger: eventId);
        }

        private static MissionSceneDefinition Scene(uint missionId, uint keypassObjective) => new()
        {
            Requirement = new CustomRequirement("character.starting-experience-completed"),
            Script = "data.sequence",
            MapContextId = Station.Map,
            ExistingFactObjectiveIds = new() { keypassObjective },
            Items = new()
            {
                new MissionItemBinding("purgas-level-3-keypass", KeypassTemplate, MissionItemScope.CharacterOwned, 1,
                    MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain,
                    Drop: new MissionItemDropDefinition(new[] { Depths.KeypassThraxThree }, keypassObjective, Station.Map, 100, 1))
            }
        };

        private static void MeansToAnEndRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, MeansToAnEnd, "A Means to an End", 4374, null, null, 42,
                PurgasStationMissionsV1.Category, radioCompleteable: true);
            mission.Reward(60000, 6000, selectableItems: Choice(11937, 14492, 47916, 47732));
            mission.Prerequisite(PurgasStationMissionsV1.OffGrid);

            var basins = (uint)Depths.Basins.Length;
            mission.Objective(1, 4378, 7595, 0, MissionObjectiveState.Incomplete, counterTextId: 4379);
            mission.Objective(2, 4410, 7596, 1);

            mission.Scenario(Events.Basins, "Purgas: fuel basins 1-7 shut down", Upto(basins));

            mission.Transition(1);
            OnEvents(mission, 1, Events.Basins, basins);
            mission.RevealAndActivate(1, 2);

            mission.Transition(2);
            mission.Progress(2, MissionProgressEventKind.ItemAcquired, KeypassClass, target: 1);
            mission.Action(2, 1, MissionActionKind.GrantReward, reward: 1);

            for (uint n = 1; n <= basins; n++)
            {
                var at = Depths.Basins[n - 1].At;
                mission.Indicator(1, Unnamed(1, n), at.X, at.Y, at.Z, 6);
            }
            mission.Indicator(2, Unnamed(2), Depths.KeypassThreeAt.X, Depths.KeypassThreeAt.Y, Depths.KeypassThreeAt.Z, 12);

            mission.Evidence(1, MissionEvidenceSourceKind.Client, "native:missionobjective/850",
                "Cease Production of Fuel at all Basins1 (counter4379 'Fuel Basins in Production'), the Level 3 Access Keypass2 (MisDivideItemPurgasLevel3Pass class20743/template42228). Texts 4374-4377, 7594.");
            mission.Evidence(2, MissionEvidenceSourceKind.Reconstruction, "tentonhammer:purgas-station",
                "Offered by HQ after Off Grid; seven fuel basins: two in the north-west room (-195, 34, 61) and (-256, 33, 64), one by the transporter (-112, 35, 33), two in the fuel pump room (-240, 33, -109), " +
                "(-18, 9, -211) and (-120, 9, -214); completed at a CommLink for 6000 credits and a weapon. Ours: radioed in; 60000 XP; physical rifle 11937, laser pistol 14492, fire grenade launcher 47916, fire leech gun 47732.");

            mission.Channels(MissionChannel.Radio, MissionChannel.Radio,
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, MeansToAnEndOfferKey, Station.Map));
            mission.Enable(Scene(MeansToAnEnd, 2));
        }

        private static void ReactorOverloadRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, ReactorOverload, "Reactor Overload", 4406, null, null, 43,
                PurgasStationMissionsV1.Category, radioCompleteable: true);
            mission.Reward(60000, 6000, selectableItems: Choice(23828, 18899, 32826, 38952));
            mission.Prerequisite(MeansToAnEnd);

            var pumps = (uint)Depths.ReactorPumps.Length;
            mission.Objective(1, 4415, 7364, 0, MissionObjectiveState.Incomplete, counterTextId: 4416);
            mission.Objective(3, 4485, 7366, 1);
            mission.Objective(2, 4418, 7365, 2);

            mission.Scenario(Events.Pumps, "Purgas: the reactor's fuel pumps 1-4 down", Upto(pumps));
            mission.Scenario(Events.Module, "Purgas: the stabilization module overdriven", Upto(1));

            mission.Transition(1);
            OnEvents(mission, 1, Events.Pumps, pumps);
            mission.RevealAndActivate(1, 3);

            mission.Transition(3);
            mission.Progress(3, MissionProgressEventKind.ItemAcquired, KeypassClass, target: 1);
            mission.RevealAndActivate(3, 2);

            mission.Transition(2);
            mission.Progress(2, MissionProgressEventKind.ScenarioEvent, Events.Overdriven, counter: Events.Module);
            mission.Action(2, 1, MissionActionKind.GrantReward, reward: 1);

            for (uint n = 1; n <= pumps; n++)
            {
                var at = Depths.ReactorPumps[n - 1];
                mission.Indicator(1, Unnamed(1, n), at.X, at.Y, at.Z, 6);
            }
            mission.Indicator(3, Unnamed(3), Depths.KeypassThreeAt.X, Depths.KeypassThreeAt.Y, Depths.KeypassThreeAt.Z, 12);
            mission.Indicator(2, Unnamed(2), Depths.ModuleAt.X, Depths.ModuleAt.Y, Depths.ModuleAt.Z, 4);

            mission.Evidence(1, MissionEvidenceSourceKind.Client, "native:missionobjective/852",
                "Destroy the Reactor's Fuel Pumps1 (counter4416 'Reactor Fuel Pumps Remaining'), Overdrive the Stabilization Module in the Control Room2, the Level 3 Access Keypass3. Texts 4406-4409, 7363.");
            mission.Evidence(2, MissionEvidenceSourceKind.Reconstruction, "tentonhammer:purgas-station",
                "Radioed in after A Means to an End; the four reactor fuel pumps on the lowest level below the core; the control panel in the Level 3 Control Room (14, 31, 86); 6000 credits and a Graviton Helmet or Bio/Mech/Stealth gloves. " +
                "Ours: 60000 XP; Graviton V02 helmet 23828, Bio V02 gloves 18899, Mech V02 gloves 32826, Stealth V02 gloves 38952 (40-44 Uncommon).");

            mission.Channels(MissionChannel.Radio, MissionChannel.Radio,
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, ReactorOverloadOfferKey, Station.Map));
            mission.Enable(Scene(ReactorOverload, 3));
        }
    }
}
