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

    /// <summary>
    /// Purgas Station's top-floor missions (Purgas_station_level_one), on the place
    /// PurgasStationContent puts down and the Game's PurgasStation runs, as Ten Ton Hammer's
    /// walkthrough tells them:
    ///  - 339 A New Plan of Attack (Recon Officer Stevens at Foreas Base): the imprisoned squad
    ///    found, a Bane Communication Link found, the squad released at the Force Field Controller,
    ///    the access code from Squad Leader Keen, and HQ contacted at the CommLink with it. Turned in
    ///    by radio: it "completes at this point".
    ///  - 704 Chain Reaction (HQ by radio once a Thrax Deathtribe Soldier is dead in the copy; to
    ///    Stevens): the five Deathtribe.
    ///  - 705 Off Grid (HQ by radio once 339 is done; radioed in): Comm. Expert Taylor's briefing, the
    ///    waypoint activated, the Level 2 Access Keypass from the drop-out room's Thrax, and the
    ///    facility explored round the reactor's top tier.
    ///
    /// The client gives the missions, their objectives, texts, counters and conversation packages:
    /// the CommLink speaks Stevens's package (586) on 339's objective 8 - flag 1 "not fully
    /// decrypted" with "Attempt to Decrypt again?" / "Enter Random Access Code?", flag 19 "Enter
    /// Required Access Code:", flag 209 the decryption device melted into it, flag 208 the code
    /// entered; Keen's 578 gives the code (131429) on 12, Taylor's 613 briefs on 705's 6. The
    /// walkthrough gives the order of things, the credits and what kind of item each gives.
    ///
    /// Each scene is the station's (MissionSceneDefinition.MapContextId), so the markers are sent there
    /// and not on the Divide, where Stevens gives 339 and takes 704.
    ///
    /// Ours: the CommLink a menu of those four pages (SceneObjectConversation.Menu) kept in a
    /// character flag, each shown on this map only (Stevens has the same package); 705's two NULL
    /// objectives (7, 8: the client's own bookkeeping) left out; the experience, at the level-44
    /// Creature Strike's ten to a credit; the items, the level 40-44 Uncommon V02 lines the
    /// walkthrough names (and 704's "armor piece" the four vests, 705's "weapon" four of the
    /// level-40 Uncommon guns). 850 A Means to an End and 852 Reactor Overload are the lower
    /// levels' and are not here.
    /// </summary>
    public static class PurgasStationMissionsV1
    {
        public const uint NewPlanOfAttack = 339, ChainReaction = 704, OffGrid = 705;

        public static readonly uint[] All = { NewPlanOfAttack, ChainReaction, OffGrid };

        public const string Revision = "purgas_1_16_5";

        /// <summary>The client's mission category: Instance (Purgas Station).</summary>
        public const uint Category = 10000036;

        public const string ChainReactionOfferKey = "purgas.chain-reaction", OffGridOfferKey = "purgas.off-grid";

        /// <summary>The client's names for map markers (missionobjectiveindicatorlanguage).</summary>
        public const uint ImprisonedSquadMarker = 127, CommLinkMarker = 147, BaneTerminalMarker = 71, ForceFieldControllerMarker = 338;

        public static uint Unnamed(uint objective) => Rasa.Structures.MissionIndicator.UnnamedFrom + objective;

        /// <summary>The Bane CommLink: UsableNPCBaneKeyboard.</summary>
        public const uint CommLinkClass = 10519;

        public const string CommLinkRole = "commlink";

        /// <summary>The scenario events the Game raises, by mission: (scenario, event).</summary>
        public static class Events
        {
            // 339: scenario 1.
            public const uint Squad = 1;
            public const uint SquadFound = 1, CommLinkFound = 2, SquadReleased = 3;

            // 704: scenario 1, Deathtribe n dead is event n.
            public const uint Deathtribe = 1;

            // 705: scenario 1.
            public const uint Facility = 1;
            public const uint WaypointActivated = 1, FarSideReached = 2;
        }

        // ---- A New Plan of Attack's CommLink ---------------------------------------------------

        public const uint ContactHq = 8, ObtainCode = 12;

        /// <summary>The CommLink's page (server flag) and whether Keen's code is known.</summary>
        public const uint PageFlag = 339001, CodeFlag = 339002;

        public const uint RootPage = 1, RandomCodePage = 2, MeltedPage = 3;

        /// <summary>The native flags of the CommLink's pages.</summary>
        public const uint RootFlag = 1, RandomCodeFlag = 19, CodeEnteredFlag = 208, MeltedFlag = 209;

        /// <summary>Objective 8's transitions: the code entered, and the pages' ways.</summary>
        public const uint Contacted = 1, ToRandomCode = 2, ToMelted = 3, BackToRoot = 4;

        public static IEnumerable<string> Up()
        {
            var sql = new List<string>();

            NewPlanOfAttackRows(sql);
            ChainReactionRows(sql);
            OffGridRows(sql);

            return sql;
        }

        public static IEnumerable<string> Down() => InstanceMissionSql.Remove(Revision, All);

        private static InstanceMissionSql.RewardItem[] Choice(params uint[] templates) =>
            templates.Select(template => new InstanceMissionSql.RewardItem(template, 1)).ToArray();

        private static uint[] Upto(uint count) => Enumerable.Range(1, (int)count).Select(n => (uint)n).ToArray();

        private static void OnEvent(InstanceMissionSql mission, uint objective, uint scenario, uint eventId, uint transition = 1) =>
            mission.Progress(objective, MissionProgressEventKind.ScenarioEvent, eventId, counter: scenario, transition: transition);

        private static void OnEvents(InstanceMissionSql mission, uint objective, uint scenario, uint count)
        {
            for (var eventId = 1U; eventId <= count; eventId++)
                mission.Progress(objective, MissionProgressEventKind.ScenarioEvent, eventId, counter: scenario, target: count, trigger: eventId);
        }

        /// <summary>A binding for the bootcamp's graduates; one whose events the Game raises runs the data sequence.</summary>
        private static MissionSceneDefinition Scene(MissionSceneDefinition scene, bool events = true)
        {
            scene.Requirement ??= new CustomRequirement("character.starting-experience-completed");
            scene.Script ??= events ? "data.sequence" : null;
            return scene;
        }

        private static readonly MapRequirement OnTheStation = new(new[] { Station.Map });

        private static MissionRequirement Page(uint page) => new AllRequirements(new MissionRequirement[]
        {
            OnTheStation, new FlagRequirement(PageFlag, page), new NotRequirement(new FlagRequirement(CodeFlag, 1))
        });

        private static void NewPlanOfAttackRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, NewPlanOfAttack, "A New Plan of Attack", 202, Station.Stevens, null, 40, Category);
            mission.Reward(80000, 8000, selectableItems: Choice(19157, 23957, 32955, 39081));
            mission.RadioTurnIn();

            mission.Objective(4, 3245, 6972, 0, MissionObjectiveState.Incomplete);
            mission.Objective(11, 9515, 9516, 1, MissionObjectiveState.Incomplete);
            mission.Objective(9, 9517, 9518, 2);
            mission.Objective(ObtainCode, 9519, 9520, 3);
            mission.Objective(ContactHq, 4313, 6975, 4);

            mission.Scenario(Events.Squad, "Purgas: the squad found, a CommLink found, the squad released", Upto(3));

            mission.Transition(4);
            OnEvent(mission, 4, Events.Squad, Events.SquadFound);
            mission.RevealAndActivate(4, 9);

            mission.Transition(11);
            OnEvent(mission, 11, Events.Squad, Events.CommLinkFound);
            mission.RevealAndActivate(11, ContactHq);

            mission.Transition(9);
            OnEvent(mission, 9, Events.Squad, Events.SquadReleased);
            mission.Action(9, 1, MissionActionKind.TakeDownField,
                itemIntent: new TakeDownFieldIntent("purgas-holding-room-field", Station.CellField));
            mission.RevealAndActivate(9, ObtainCode, 2);

            mission.Transition(ObtainCode);
            mission.Conversation(ObtainCode, Station.KeenPackage);
            mission.SetFlag(ObtainCode, 1, CodeFlag, 1);

            // The CommLink: the code entered completes it; the pages' choices only turn its page.
            mission.Transition(ContactHq, Contacted);
            mission.Conversation(ContactHq, Station.StevensPackage, CodeEnteredFlag, Contacted);
            mission.Action(ContactHq, 1, MissionActionKind.GrantReward, reward: 1, transition: Contacted);
            foreach (var (transition, flag, page) in new[]
            {
                (ToRandomCode, RootFlag, RandomCodePage), (ToMelted, RootFlag, MeltedPage), (BackToRoot, RandomCodeFlag, RootPage)
            })
            {
                mission.Transition(ContactHq, transition, MissionObjectiveState.Incomplete, transition);
                mission.Conversation(ContactHq, Station.StevensPackage, flag, transition);
                mission.SetFlag(ContactHq, 1, PageFlag, page, transition);
            }

            mission.Indicator(4, ImprisonedSquadMarker, Station.KeenAt.X, Station.KeenAt.Y, Station.KeenAt.Z, 10);
            mission.Indicator(11, CommLinkMarker, Station.CommLinkAt.X, Station.CommLinkAt.Y, Station.CommLinkAt.Z, 6);
            mission.Indicator(9, ForceFieldControllerMarker, Station.ReleaseAt.X, Station.ReleaseAt.Y, Station.ReleaseAt.Z, 4);
            mission.Indicator(ObtainCode, Unnamed(ObtainCode), Station.KeenAt.X, Station.KeenAt.Y, Station.KeenAt.Z, 4);
            mission.Indicator(ContactHq, CommLinkMarker, Station.CommLinkAt.X, Station.CommLinkAt.Y, Station.CommLinkAt.Z, 4);

            mission.Evidence(1, MissionEvidenceSourceKind.Client, "native:objectiveconversation/339",
                "Stevens586: locate the squad leader4, a CommLink11, release the squad9, the access code12 (Keen578: 131429), HQ via the CommLink8 on package586: " +
                "flag1 choice body (decrypt again / random code), flag19 'Enter Required Access Code:', flag209 the device melted, flag208 the code entered. Texts 202-205, 6971.");
            mission.Evidence(2, MissionEvidenceSourceKind.Reconstruction, "tentonhammer:purgas-station",
                "Holding room left of the entrance corridor behind a force field; CommLink at the control room's north end (160, 73, 130), 'not fully decrypted'; " +
                "the release terminal (158, 73, 133); Keen at (192, 65, 153) gives the code; the CommLink with it completes: 8000 credits, Bio/Graviton Legs or Mech/Stealth Helmet. " +
                "Ours: 80000 XP; Bio V02 legs 19157, Graviton V02 legs 23957, Mech V02 helmet 32955, Stealth V02 helmet 39081 (40-44 Uncommon).");

            var scene = Scene(new MissionSceneDefinition
            {
                MapContextId = Station.Map,
                BoundMapContextId = Station.Map,
                Dialogue = new()
                {
                    new(ContactHq, Station.StevensPackage, RootFlag, MissionDialogueKind.Choice,
                        choices: new Dictionary<int, uint> { [1] = ToMelted, [2] = ToRandomCode }, dialogObjectiveId: ContactHq,
                        requirement: Page(RootPage)),
                    new(ContactHq, Station.StevensPackage, RandomCodeFlag, MissionDialogueKind.Choice,
                        choices: new Dictionary<int, uint> { [1] = BackToRoot, [2] = BackToRoot }, dialogObjectiveId: ContactHq,
                        requirement: Page(RandomCodePage)),
                    new(ContactHq, Station.StevensPackage, MeltedFlag, MissionDialogueKind.Ambient, dialogObjectiveId: ContactHq,
                        requirement: Page(MeltedPage)),
                    new(ContactHq, Station.StevensPackage, CodeEnteredFlag, transitionId: Contacted, dialogObjectiveId: ContactHq,
                        requirement: new AllRequirements(new MissionRequirement[] { OnTheStation, new FlagRequirement(CodeFlag, 1) }))
                }
            });
            scene.Actors[CommLinkRole] = new SceneActorDefinition(CommLinkRole, SceneActorKind.Object, CommLinkClass,
                new ScenePosition((float)Station.CommLinkAt.X, (float)Station.CommLinkAt.Y, (float)Station.CommLinkAt.Z), Station.CommLinkYaw,
                Conversation: new SceneObjectConversation(NewPlanOfAttack, ContactHq, Station.StevensPackage, ContactHq, RootFlag,
                    MissionDialogueKind.Choice, Menu: true));
            scene.Sequences[0] = new SceneSequenceDefinition
            {
                World = { new EnsureActorIntent("ensure-commlink", CommLinkRole) },
                Character =
                {
                    new SetCharacterFlagIntent("commlink-at-root", PageFlag, RootPage),
                    new SetCharacterFlagIntent("code-not-known", CodeFlag, null)
                }
            };
            mission.Enable(scene);
        }

        private static void ChainReactionRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, ChainReaction, "Chain Reaction", 3250, null, Station.Stevens, 43, Category);
            mission.Reward(61500, 6150, selectableItems: Choice(19286, 24086, 33213, 39340));

            mission.Objective(1, 3254, 7439, 0, MissionObjectiveState.Incomplete, counterTextId: 3255);

            mission.Scenario(Events.Deathtribe, "Purgas: Thrax Deathtribe Soldiers 1-5 dead", Upto((uint)Station.DeathtribeSpots.Length));

            mission.Transition(1);
            OnEvents(mission, 1, Events.Deathtribe, (uint)Station.DeathtribeSpots.Length);
            mission.Action(1, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Evidence(1, MissionEvidenceSourceKind.Client, "native:missionobjective/704",
                "Eliminate All Members of the Deathtribe Squad1 (counter3255 'Thrax Deathtribe Squad Killed'); return to Stevens. Texts 3250-3253, 7438.");
            mission.Evidence(2, MissionEvidenceSourceKind.Reconstruction, "tentonhammer:purgas-station",
                "Radioed in on killing the first level 45 Deathtribe; five, through the facility; Stevens completes it for 6150 credits and an armor piece. " +
                "Ours: 61500 XP; Bio 19286, Graviton 24086, Mech 33213, Stealth 39340 V02 vests (40-44 Uncommon); the kills the copy's, caught up to whoever takes it later.");

            mission.Channels(MissionChannel.Radio, MissionChannel.Npc,
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, ChainReactionOfferKey, Station.Map));
            mission.Enable(Scene(new MissionSceneDefinition()));
        }

        private static void OffGridRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, OffGrid, "Off Grid", 3256, null, null, 40, Category, radioCompleteable: true);
            mission.Reward(40000, 800, selectableItems: Choice(11947, 14409, 47980, 45763));
            mission.Prerequisite(NewPlanOfAttack);

            mission.Objective(6, 4333, 6984, 0, MissionObjectiveState.Incomplete);
            mission.Objective(10, 4339, 6986, 1);
            mission.Objective(11, 4355, 6988, 2);
            mission.Objective(9, 4338, 6987, 3);

            mission.Scenario(Events.Facility, "Purgas: the waypoint activated, the far side reached", Upto(2));

            mission.Transition(6);
            mission.Conversation(6, Station.TaylorPackage);
            mission.RevealAndActivate(6, 10);

            mission.Transition(10);
            OnEvent(mission, 10, Events.Facility, Events.WaypointActivated);
            mission.RevealAndActivate(10, 11);

            mission.Transition(11);
            mission.Progress(11, MissionProgressEventKind.ItemAcquired, KeypassClass, target: 1);
            mission.RevealAndActivate(11, 9);

            mission.Transition(9);
            OnEvent(mission, 9, Events.Facility, Events.FarSideReached);
            mission.Action(9, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Indicator(6, Unnamed(6), Station.TaylorAt.X, Station.TaylorAt.Y, Station.TaylorAt.Z, 4);
            mission.Indicator(10, BaneTerminalMarker, Station.TerminalAt.X, Station.TerminalAt.Y, Station.TerminalAt.Z, 4);
            mission.Indicator(11, Unnamed(11), Station.DropOutRoomAt.X, Station.DropOutRoomAt.Y, Station.DropOutRoomAt.Z, 10);
            mission.Indicator(9, Unnamed(9), Station.FarSideAt.X, Station.FarSideAt.Y, Station.FarSideAt.Z, FarSideRadius);

            mission.Evidence(1, MissionEvidenceSourceKind.Client, "native:missionobjective/705",
                "Taylor613: brief6, await waypoint activation10, the Level 2 Access Keypass11 (MisDivideItemPurgasLevel2Pass class20745/template42229), explore further9; " +
                "NULL7 'starts objective' and NULL8 'repair waypoint' left out. Texts 3256-3259, 6982.");
            mission.Evidence(2, MissionEvidenceSourceKind.Reconstruction, "tentonhammer:purgas-station",
                "Radioed in after 339; Taylor ports in at (150, 73, 131); the terminal at (150, 63, 82) activates the waypoint to Level 2; the keypass from a Thrax in the drop-out room at (108, 68, -2); " +
                "round the green hallway to the opposite side updates it; radioed in for 800 credits and a weapon. Ours: 40000 XP; laser rifle 11947, physical pistol 14409, physical grenade launcher 47980, physical leech gun 45763.");

            mission.Channels(MissionChannel.Radio, MissionChannel.Radio,
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, OffGridOfferKey, Station.Map));
            mission.Enable(Scene(new MissionSceneDefinition
            {
                MapContextId = Station.Map,
                ExistingFactObjectiveIds = new() { 11 },
                Items = new()
                {
                    new MissionItemBinding("purgas-level-2-keypass", KeypassTemplate, MissionItemScope.CharacterOwned, 1,
                        MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain,
                        Drop: new MissionItemDropDefinition(new[] { Station.KeypassThrax }, 11, Station.Map, 100, 1))
                }
            }));
        }

        /// <summary>The Level 2 Access Keypass: MisDivideItemPurgasLevel2Pass.</summary>
        public const uint KeypassClass = 20745, KeypassTemplate = 42229;

        /// <summary>How near the far side's middle counts as having gone round to it.</summary>
        public const double FarSideRadius = 16;
    }
}
