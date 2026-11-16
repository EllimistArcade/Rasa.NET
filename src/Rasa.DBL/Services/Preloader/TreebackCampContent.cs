using System.Collections.Generic;
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
    using static AliaDasNpcs;

    /// <summary>
    /// The Treeback Camp (adv_foreas_concordia_palisades_treebackcamp, 1397), off the Palisades, as
    /// the Tabula Rasa wiki's page and Ten Ton Hammer tell it: the AFS taking back the beachhead and
    /// the forward base, the Bane's camp behind its gate, and its thirteen missions, in the client's
    /// own names and texts.
    ///
    /// Who and what:
    ///  - Field Lieutenant Bagby at the beachhead by the medic camp, who takes Treeback Beachhead
    ///    (Colonel Almos at Viands Village on the Palisades) and gives Retake the Forward Base: the
    ///    forward base's computer turned on, then Bagby comes up to it. There he gives Treeback
    ///    Research, which is Sergeant Louden at the valley's mouth, with Rangers Zamir and Oleev.
    ///  - Louden's Open the Gate (the force field controller in the south-west pillbox shuts the
    ///    gate's field) and Check the Ridge (the signal from the north bunker, where Squad Leader
    ///    Keen lies dying; he gives Howling Revenge, eight howlers of the kennels).
    ///  - Lieutenant Galloway at the gate gives Cut Off The Head... (Researcher Amuaggor in the
    ///    Treeback Pens, Hunter Lord Cromak east of the gate, Security Chief Thraggorth, whom the
    ///    generator station's control panel lures out), radioed in; Finishing Move (Base Commander
    ///    Tagak in his command post, there once the three are dead) is offered by radio after it.
    ///  - Commander Kendrick lands inside the gate once it is open and gives Sabotage: the power
    ///    conduits at the foot of the five sonic towers on the ridge, one after the other; radioed
    ///    in. Captain Markinson lands in the generator station once it is secure and gives Treeback
    ///    Genocide: NX-22 into the Bane gas harvester in the Treeback Pens, then Bagby.
    ///  - From outside: Medical Alert Badge (Field Medic Olaera, Viands Village: the badge off
    ///    Amuaggor), A Bottle of the Good Stuff (Captain Tayros at Fort Dew, to Bagby) and Treeback
    ///    Field Report (Sergeant Longshanks, Viands Village, to Galloway).
    ///  - The Bane at levels 22 to 26, in the client's own Treeback names: guards, security,
    ///    hunters with their howlers, the kennels' alpha howlers, researchers, Hominis Machina,
    ///    snipers, and the pens' altered treebacks; the four leaders.
    /// The computer, the gate's field and its controller, the conduits, the generator station's
    /// panel, the harvester, Bagby, Kendrick's landing, Markinson's, the leaders and the scenario
    /// events the missions wait for are the server's, per copy of the camp (TreebackCamp).
    ///
    /// The wiki's known bugs, put right: every objective has its marker, the gate's controller
    /// among them; a leader killed before his mission was taken counts for it; Howling Revenge
    /// counts every howler, of whatever kind (SPECIES_HOWLER); Cut Off The Head's three are
    /// taken in any order.
    /// </summary>
    public static class TreebackCampContent
    {
        public const string Revision = "treeback_1_16_5";
        public const uint Camp = 1397, Palisades = 1244;

        /// <summary>missioncategorylanguage: "Instance (Treeback Camp)".</summary>
        public const uint Category = 10000042;

        public const uint TreebackBeachhead = 551, TreebackResearch = 861, RetakeTheForwardBase = 862, OpenTheGate = 863,
            CheckTheRidge = 865, CutOffTheHead = 866, HowlingRevenge = 868, Sabotage = 869, TreebackGenocide = 870,
            FinishingMove = 1019, MedicalAlertBadge = 1787, BottleOfTheGoodStuff = 1788, TreebackFieldReport = 1789;

        public static readonly uint[] Missions =
        {
            TreebackBeachhead, RetakeTheForwardBase, TreebackResearch, OpenTheGate, CheckTheRidge, HowlingRevenge, CutOffTheHead,
            FinishingMove, Sabotage, TreebackGenocide, MedicalAlertBadge, BottleOfTheGoodStuff, TreebackFieldReport
        };

        // The NPC packages of the missions' conversations (objectiveconversation).
        public const uint BagbyPackage = 46, GallowayPackage = 565, LoudenPackage = 571;

        // People already in the world: Captain Tayros at Fort Dew.
        public const uint Tayros = 510122;

        // New people and rows.
        public const uint Galloway = 785001, Louden = 785002, Keen = 785003, Zamir = 785004, Oleev = 785005, Bagby = 785006,
            Kendrick = 785007, Starks = 785008, Brooks = 785009, Markinson = 785010, Almos = 785011, Longshanks = 785012, Olaera = 785013;
        public const uint Guard = 785020, Security = 785021, Hunter = 785022, AlphaHowler = 785023, Researcher = 785024, Machina = 785025,
            Sniper = 785026, AlteredTreeback = 785027;
        public const uint Amuaggor = 785030, Cromak = 785031, Thraggorth = 785032, Tagak = 785033;

        /// <summary>The camp's AFS: those the copy puts down (TreebackCamp) and those that stand in pools.</summary>
        public static readonly uint[] Afs = { Galloway, Louden, Keen, Zamir, Oleev, Bagby, Kendrick, Starks, Brooks, Markinson };

        /// <summary>The four the copy puts down and keeps track of (TreebackCamp).</summary>
        public static readonly uint[] Leaders = { Amuaggor, Cromak, Thraggorth, Tagak };

        /// <summary>A Caretaker of the Palisades' (level 24), in the pens with the treebacks.</summary>
        public const uint PalisadesCaretaker = 531066;

        // Items (class, template).
        public const uint NerveGasClass = 10453, NerveGasTemplate = 2320;          // MisPalisadesTreebackCalmingAgent, "NX-22 Gas"
        public const uint LiquorClass = 28425, LiquorTemplate = 118784;            // MisTreebackBottleOfLiquor
        public const uint ReportClass = 28426, ReportTemplate = 118785;            // MisTreebackFieldReport
        public const uint BadgeClass = 28427, BadgeTemplate = 118786;              // MisTreebackMedicalBracelet

        // The objects the Game puts down.
        public const uint ComputerClass = 9478;     // UsableTwoStateHumComputerV01V06, "Computer Terminal"
        public const uint GateSwitchClass = 7468;   // UsableTwoStateBaneConsoleSwitchV01, "Switch"
        public const uint PanelClass = 4424;        // UsableTwoStateBanePedastalSwitchV01, on the station's console pedestal
        public const uint HarvesterClass = 7908;    // UsableItemAccBaneGasHarvesterV01, "Gas Harvester"
        public const uint ConduitClass = 7158;      // UsableInertDestTreebackpoweconduitDELETEDUPE, "Power Conduit"

        // usablenameoverridelanguage.
        public const uint GateSwitchName = 241, PanelName = 119;   // "Force Field Controller", "Generator Control Panel"

        public const uint ConduitHitPoints = 1500;

        // missionobjectiveindicatorlanguage.
        public const uint EntranceMarker = 379, ComputerMarker = 329, ControllerMarker = 338, ConduitMarker = 97, HarvesterMarker = 106;

        // Objectives (missionobjective), and the scenario every mission's events are in.
        public const uint Scenario = 1;
        public const uint ReportToBagby = 1;                                            // 551
        public const uint SpeakWithLouden = 1;                                          // 861
        public const uint TurnOnTheComputer = 1, WaitForBagby = 2;                      // 862
        public const uint ShutOffTheField = 1;                                          // 863
        public const uint FindTheSignal = 1, TalkToTheCommando = 2;                     // 865
        public const uint KillResearcher = 1, KillHunterLord = 2, KillSecurityChief = 3; // 866
        public const uint KillAlphaHowlers = 1;                                         // 868
        public const uint AllConduits = 9;                                              // 869
        public const uint DeployTheAgent = 5;                                           // 870
        public const uint KillTheCommander = 4;                                         // 1019
        public const uint GetTheBadge = 1, FindBagby = 1, DeliverTheReport = 1;          // 1787, 1788, 1789

        // Events.
        public const uint ComputerOn = 1, BagbyArrived = 2;
        public const uint GateOpen = 1;
        public const uint SignalFound = 1, CommandoFound = 2;
        public const uint ResearcherDead = 1, HunterLordDead = 2, SecurityChiefDead = 3;
        public const uint AgentDeployed = 1;
        public const uint CommanderDead = 1;

        /// <summary>Howlers of every kind (creature_class_flag SPECIES_HOWLER): the howlers, the alphas and the hunters' pets.</summary>
        public const uint HowlerSpecies = 80;
        public const uint HowlersWanted = 8;

        /// <summary>A power conduit: its objective of Sabotage (its event is its place in the line, 1 to 5), the tower it powers, and where.</summary>
        public sealed record Conduit(uint Objective, uint Name, uint Body, Vector3 Tower, Vector3 At)
        {
            /// <summary>Facing the tower it is at the foot of.</summary>
            public float Yaw => (float)System.Math.Atan2(Tower.X - At.X, Tower.Z - At.Z);
        }

        /// <summary>The five, in the line along the ridge the walkthrough takes: the first above the kennels, the last north-west of the generator station.</summary>
        public static readonly Conduit[] Conduits =
        {
            new(8, 4553, 8176, new Vector3(48.7f, 156.8f, 277f), new Vector3(48.7f, 156.85f, 272f)),
            new(10, 4554, 8177, new Vector3(-34.2f, 172.5f, 423.8f), new Vector3(-30.8f, 173.2f, 422f)),
            new(11, 4555, 8178, new Vector3(58.1f, 160.2f, 484.3f), new Vector3(51.2f, 161.2f, 480.4f)),
            new(12, 4556, 8179, new Vector3(170.2f, 165.3f, 443.8f), new Vector3(175.17f, 167f, 440.72f)),
            new(13, 4557, 8180, new Vector3(233.6f, 187.1f, 324.1f), new Vector3(230.1f, 187.33f, 320.6f))
        };

        /// <summary>The way in, from the Palisades (map_link 21), and where a player arrives.</summary>
        public static readonly Vector3 Arrival = new(-337.4749f, 103.7221f, 398.6607f);

        /// <summary>The Palisades' side of the way in, by the camp's mouth.</summary>
        public static readonly Vector3 CampMouth = new(656.1f, 145.84f, -921.4f);

        public static readonly Vector3 AlmosAt = new(-238.5f, 178.08f, -543.8f);
        public static readonly Vector3 LongshanksAt = new(-238.5f, 178.25f, -547.5f);
        public static readonly Vector3 OlaeraAt = new(-232f, 178.34f, -548f);

        public static readonly Vector3 BagbyAt = new(-287f, 100.26f, 281f);
        public static readonly Vector3 ForwardBase = new(-385f, 105.81f, 272f);
        public static readonly Vector3 BagbyAtForwardBase = new(-392.4f, 105.8f, 272f);
        public static readonly Vector3 ComputerAt = new(-398.4f, 105.83f, 266.3f);
        public static readonly Vector3 LoudenAt = new(-200f, 103.01f, 143f);
        public static readonly Vector3 ZamirAt = new(-196f, 105.31f, 147f);
        public static readonly Vector3 OleevAt = new(-204f, 101.58f, 139f);
        public static readonly Vector3 GateSwitchAt = new(-206.8f, 116.6f, 50.6f);
        public const float GateSwitchYaw = 1.49f;
        public static readonly Vector3 GallowayAt = new(-130f, 99.36f, 124f);
        public static readonly Vector3 KeenAt = new(-126.7f, 120.4f, 217f);

        /// <summary>
        /// The gate: the gap in the camp's west wall (x -118) between z 127 and 133, the only way east.
        /// A Bane outpost's minor-entrance field (13.7 m) across it, its ends in the wall on either side.
        /// </summary>
        public static readonly Vector3 GateAt = new(-118f, 100.8f, 124f);
        public const float GateYaw = 1.5707964f;
        public static readonly Vector3 GateWest = new(-124f, 100.43f, 130f), GateEast = new(-112f, 101.01f, 130f);

        public static readonly Vector3 KendrickAt = new(-106f, 102.04f, 138f);
        public static readonly Vector3 StarksAt = new(-104f, 100.86f, 134f);
        public static readonly Vector3 BrooksAt = new(-104f, 102.08f, 142f);
        public static readonly Vector3 CromakAt = new(-25.8f, 104.79f, 144.4f);
        public static readonly Vector3 Kennels = new(64f, 108.6f, 106f);
        public static readonly Vector3 AmuaggorAt = new(114f, 124.2f, 391.6f);
        public static readonly Vector3 HarvesterAt = new(120f, 121.14f, 405f);
        public static readonly Vector3 PanelAt = new(307.2f, 151f, 274.9f);
        public const float PanelYaw = 3.13f;
        public static readonly Vector3 ThraggorthAt = new(276.8f, 142.84f, 290.3f);
        public static readonly Vector3 GeneratorStation = new(318f, 151.12f, 245f);
        public static readonly Vector3 MarkinsonAt = new(320f, 151.73f, 250f);
        public static readonly Vector3 TagakAt = new(222.5f, 128.58f, -179.1f);

        public enum TargetCategoryRow : uint { Hostile = 0, Friendly = 1 }

        public sealed record Beast(uint Id, string Comment, uint ClassId, TargetCategoryRow Faction, uint Level, uint Hp,
            uint Armor, uint NameId, uint RunSpeed, uint WalkSpeed, params uint[] Actions);

        public sealed record Act(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint Windup, uint MinDamage, uint MaxDamage, uint DamageType);

        public sealed record Spot(uint Id, uint Map, double X, double Y, double Z, double Rotation, uint RespawnTenths, double Radius,
            params (uint Creature, uint Min, uint Max)[] Slots);

        private const uint ThraxPistolClass = 20757, GrenadierClass = 9354, TechnicianClass = 7043, HunterClass = 10166,
            HowlerClass = 7336, MachinaClass = 3868, LightbenderClass = 7120, BaneTreebackClass = 9750,
            TechnicianBossClass = 10502, HunterBossClass = 10327, RifleBossClass = 10504;

        // The base world's attacks: the Palisades' Thrax, grenadiers, hunters, howlers, technicians, treebacks and
        // commanders (levels 23 to 26), the Machina's revive, a Lightbender Infiltrator's beam.
        private const uint ThraxKick = 53097, ThraxPistol = 53098, GrenadierLob = 53099, GrenadierRifle = 53100,
            HunterNet = 53103, HunterRifle = 53104, HunterPet = 65035, HowlerSonic = 55011, HowlerBite = 55012, HowlerDeath = 60004,
            TechnicianGun = 70026, TechnicianHeal = 62045, TechnicianTurret = 65014, TechnicianRevive = 68079,
            TreebackStomp = 53107, TreebackHowl = 53108, TreebackSwarm = 56002, CommanderKick = 53110, CommanderRifle = 53111,
            MachinaRevive = 62059, LightbenderBeam = 781115;

        public const uint MachinaGun = 785101;

        public static readonly Act[] Actions =
        {
            new(MachinaGun, "Treeback Hominis Machina 1/97 (level 23)", 1, 97, 0.5, 25, 500, 0, 22, 46, 6)
        };

        public static readonly Beast[] Creatures =
        {
            new(Galloway, "Lt. Galloway - Treeback Camp", SwapsetFemale, TargetCategoryRow.Friendly, 25, 3000, 0, 6705, 0, 0),
            new(Louden, "Sgt. Louden - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 24, 3000, 0, 6713, 0, 0),
            new(Keen, "Squad Leader Keen, the dying commando - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 6721, 0, 0),
            new(Zamir, "Ranger Zamir - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 6722, 0, 0),
            new(Oleev, "Ranger Oleev - Treeback Camp", SwapsetFemale, TargetCategoryRow.Friendly, 23, 3000, 0, 6723, 0, 0),
            new(Bagby, "Field Lt. Bagby - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 25, 3000, 0, 2964, 9, 5),
            new(Kendrick, "Cmdr. Kendrick - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 26, 3000, 0, 6714, 0, 0),
            new(Starks, "Field Officer Starks - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 6724, 0, 0),
            new(Brooks, "Field Officer Brooks - Treeback Camp", SwapsetFemale, TargetCategoryRow.Friendly, 23, 3000, 0, 6725, 0, 0),
            new(Markinson, "Captain Markinson - Treeback Camp", SwapsetMale, TargetCategoryRow.Friendly, 26, 3000, 0, 6715, 0, 0),
            new(Almos, "Col. Almos - Viands Village", SwapsetMale, TargetCategoryRow.Friendly, 26, 3000, 0, 2965, 0, 0),
            new(Longshanks, "Sgt. Longshanks - Viands Village", SwapsetMale, TargetCategoryRow.Friendly, 24, 3000, 0, 10205, 0, 0),
            new(Olaera, "Field Medic Olaera - Viands Village", SwapsetFemale, TargetCategoryRow.Friendly, 24, 3000, 0, 10204, 0, 0),
            new(Guard, "Treeback Guard - Treeback Camp", ThraxPistolClass, TargetCategoryRow.Hostile, 23, 1446, 105, 10101, 9, 5, ThraxKick, ThraxPistol),
            new(Security, "Treeback Security - Treeback Camp", GrenadierClass, TargetCategoryRow.Hostile, 23, 1446, 105, 10102, 9, 5, GrenadierLob, GrenadierRifle),
            new(Hunter, "Treeback Hunter - Treeback Camp", HunterClass, TargetCategoryRow.Hostile, 23, 1446, 105, 10103, 9, 5, HunterNet, HunterRifle, HunterPet),
            new(AlphaHowler, "Alpha Beast Howler - Treeback Camp kennels", HowlerClass, TargetCategoryRow.Hostile, 23, 1446, 105, 8153, 9, 5,
                HowlerSonic, HowlerBite, HowlerDeath),
            new(Researcher, "Treeback Researcher - Treeback Camp", TechnicianClass, TargetCategoryRow.Hostile, 24, 2102, 165, 10105, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Machina, "Treeback Hominis Machina - Treeback Camp", MachinaClass, TargetCategoryRow.Hostile, 23, 1446, 105, 10106, 9, 5, MachinaGun, MachinaRevive),
            new(Sniper, "Treeback Sniper - Treeback Camp", LightbenderClass, TargetCategoryRow.Hostile, 22, 1250, 100, 10107, 9, 5, LightbenderBeam),
            new(AlteredTreeback, "Altered Treeback - Treeback Camp pens", BaneTreebackClass, TargetCategoryRow.Hostile, 25, 4000, 220, 0, 9, 5,
                TreebackStomp, TreebackHowl, TreebackSwarm),
            new(Amuaggor, "Researcher Amuaggor - Treeback Camp", TechnicianBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 6702, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Cromak, "Hunter Lord Cromak - Treeback Camp", HunterBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 6703, 9, 5, HunterNet, HunterRifle, HunterPet),
            new(Thraggorth, "Security Chief Thraggorth - Treeback Camp", RifleBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 6704, 9, 5, CommanderKick, CommanderRifle),
            new(Tagak, "Base Commander Tagak - Treeback Camp", RifleBossClass, TargetCategoryRow.Hostile, 26, 9000, 170, 6701, 9, 5, CommanderKick, CommanderRifle)
        };

        // Looks: the AFS officers' blue of the seed's officers; the field medic in white.
        public const uint FemaleFace = 24009, FemaleHair = 3672;

        private static Slot[] Uniform(uint hair, uint face, uint skin, uint weapon) => new[]
        {
            new Slot(2, OfficerBoots, OfficerBlue), new Slot(13, weapon, 1), new Slot(14, hair, DarkHair),
            new Slot(15, OfficerTunic, OfficerBlue), new Slot(16, OfficerLegs, OfficerBlue), new Slot(17, face, skin)
        };

        public static readonly (uint Creature, Slot[] Look)[] Looks =
        {
            (Galloway, Uniform(FemaleHair, FemaleFace, SkinLight, RifleLaser)),
            (Louden, Uniform(9782, 24032, SkinMedium, RifleAvatar)),
            (Keen, Uniform(3663, 24020, SkinLight, MachineGun)),
            (Zamir, Uniform(9784, 24032, SkinMedium, RifleLaser)),
            (Oleev, Uniform(FemaleHair, FemaleFace, SkinMedium, RifleAvatar)),
            (Bagby, Uniform(9784, 24020, SkinLight, PistolRedshirt)),
            (Kendrick, Uniform(3663, 24032, SkinMedium, MachineGun)),
            (Starks, Uniform(9782, 24020, SkinMedium, RifleAvatar)),
            (Brooks, Uniform(FemaleHair, FemaleFace, SkinLight, RifleAvatar)),
            (Markinson, Uniform(9782, 24032, SkinLight, PistolRedshirt)),
            (Almos, Uniform(3663, 24032, SkinLight, PistolRedshirt)),
            (Longshanks, Uniform(9784, 24020, SkinMedium, RifleAvatar)),
            (Olaera, new[]
            {
                new Slot(2, MedicBoots, White), new Slot(3, MedicGloves, White), new Slot(14, FemaleHair, DarkHair),
                new Slot(15, MedicTorso, White), new Slot(16, MedicLegs, White), new Slot(17, FemaleFace, SkinMedium)
            })
        };

        /// <summary>Who speaks which package.</summary>
        public static readonly (uint Creature, uint Package, string Comment)[] Packages =
        {
            (Bagby, BagbyPackage, "Field Lt. Bagby - Treeback Camp"),
            (Galloway, GallowayPackage, "Lt. Galloway - Treeback Camp"),
            (Louden, LoudenPackage, "Sgt. Louden - Treeback Camp")
        };

        public const uint NpcRespawn = 200, HostileRespawn = 3000;

        public const uint GallowayPool = 785201, LoudenPool = 785202, KeenPool = 785203, ZamirPool = 785204, OleevPool = 785205,
            AlmosPool = 785206, LongshanksPool = 785207, OlaeraPool = 785208;

        /// <summary>The camp's pools, and the three at Viands Village on the Palisades.</summary>
        public static readonly Spot[] Pools =
        {
            // The AFS.
            new(GallowayPool, Camp, GallowayAt.X, GallowayAt.Y, GallowayAt.Z, 1.6, NpcRespawn, 0, (Galloway, 1, 1)),
            new(LoudenPool, Camp, LoudenAt.X, LoudenAt.Y, LoudenAt.Z, 3.1, NpcRespawn, 0, (Louden, 1, 1)),
            new(KeenPool, Camp, KeenAt.X, KeenAt.Y, KeenAt.Z, 3.1, NpcRespawn, 0, (Keen, 1, 1)),
            new(ZamirPool, Camp, ZamirAt.X, ZamirAt.Y, ZamirAt.Z, 2.4, NpcRespawn, 0, (Zamir, 1, 1)),
            new(OleevPool, Camp, OleevAt.X, OleevAt.Y, OleevAt.Z, 2.4, NpcRespawn, 0, (Oleev, 1, 1)),
            new(AlmosPool, Palisades, AlmosAt.X, AlmosAt.Y, AlmosAt.Z, 1.6, NpcRespawn, 0, (Almos, 1, 1)),
            new(LongshanksPool, Palisades, LongshanksAt.X, LongshanksAt.Y, LongshanksAt.Z, 1.6, NpcRespawn, 0, (Longshanks, 1, 1)),
            new(OlaeraPool, Palisades, OlaeraAt.X, OlaeraAt.Y, OlaeraAt.Z, 0, NpcRespawn, 0, (Olaera, 1, 1)),
            // The Thrax holding the forward base.
            new(785210, Camp, -380, 105.99, 268, 1.6, HostileRespawn, 2, (Guard, 2, 2)),
            new(785211, Camp, -372, 106.66, 282, 1.6, HostileRespawn, 2, (Security, 1, 1), (Guard, 1, 1)),
            // The force field controller's pillbox.
            new(785212, Camp, -200.63, 117.89, 59.59, 3.1, HostileRespawn, 1, (Guard, 2, 2)),
            // The kennels, and Cromak's hunters east of the gate.
            new(785213, Camp, 56, 108.6, 106, 0, HostileRespawn, 4, (AlphaHowler, 3, 3)),
            new(785214, Camp, 72, 108.6, 106, 0, HostileRespawn, 4, (AlphaHowler, 2, 2), (Hunter, 1, 1)),
            new(785215, Camp, 40, 108.6, 106, 0, HostileRespawn, 3, (AlphaHowler, 2, 2)),
            new(785216, Camp, -20, 104.42, 150, 1.6, HostileRespawn, 3, (Hunter, 2, 2)),
            // Amuaggor's escort in the Treeback Pens, and the pens' treebacks.
            new(785217, Camp, 108, 125.26, 386, 3.1, HostileRespawn, 3, (Researcher, 1, 1), (Guard, 2, 2)),
            new(785218, Camp, 120, 123.37, 396, 3.1, HostileRespawn, 3, (Security, 2, 2)),
            new(785219, Camp, 106, 123.31, 398, 3.1, HostileRespawn, 3, (Sniper, 2, 2)),
            new(785220, Camp, 90, 121.5, 420, 0, HostileRespawn, 6, (AlteredTreeback, 2, 2)),
            new(785221, Camp, 140, 119.07, 420, 0, HostileRespawn, 6, (AlteredTreeback, 2, 2)),
            new(785222, Camp, 80, 124.83, 370, 0, HostileRespawn, 5, (AlteredTreeback, 1, 1), (PalisadesCaretaker, 1, 1)),
            // The generator station: secured, Markinson lands.
            new(785223, Camp, 300, 151.1, 240, 1.6, HostileRespawn, 3, (Machina, 2, 2)),
            new(785224, Camp, 335, 150.96, 230, -1.6, HostileRespawn, 3, (Guard, 2, 2)),
            new(785225, Camp, 300, 151.06, 225, 1.6, HostileRespawn, 3, (Researcher, 1, 1), (Security, 1, 1)),
            // Tagak's command post, the ridge's snipers and the southern lake's Machina.
            new(785226, Camp, 230, 128.2, -170, 1.6, HostileRespawn, 2, (Security, 2, 2)),
            new(785227, Camp, -79.61, 158.09, 287.26, 1.6, HostileRespawn, 2, (Sniper, 2, 2)),
            new(785228, Camp, 175, 112.74, -110, 0, HostileRespawn, 4, (Machina, 2, 2))
        };

        #region Up and Down

        public static IEnumerable<string> Up()
        {
            foreach (var a in Actions)
                yield return "INSERT INTO creature_action (id, description, action_id, action_arg_id, range_min, range_max, cooldown, windup, min_damage, max_damage, damage_type) " +
                    $"SELECT {a.Id}, {S(a.Description)}, {a.ActionId}, {a.ArgId}, {N(a.RangeMin)}, {N(a.RangeMax)}, {a.Cooldown}, {a.Windup}, {a.MinDamage}, {a.MaxDamage}, {a.DamageType} " +
                    $"FROM entityclass e WHERE e.id = {MachinaClass} AND NOT EXISTS (SELECT 1 FROM creature_action x WHERE x.id = {a.Id});";

            foreach (var c in Creatures)
            {
                var actions = c.Actions.Concat(Enumerable.Repeat(0U, 8)).Take(8).ToArray();
                var actionsThere = string.Concat(c.Actions.Select(id => $" AND EXISTS (SELECT 1 FROM creature_action a WHERE a.id = {id})"));

                yield return "INSERT INTO creature (id, comment, class_id, faction, level, max_hp, name_id, run_speed, walk_speed, " +
                    "action1, action2, action3, action4, action5, action6, action7, action8) " +
                    $"SELECT {c.Id}, {S(c.Comment)}, {c.ClassId}, {(uint)c.Faction}, {c.Level}, {c.Hp}, {c.NameId}, {c.RunSpeed}, {c.WalkSpeed}, {string.Join(", ", actions)} " +
                    $"FROM entityclass e WHERE e.id = {c.ClassId}{actionsThere} AND NOT EXISTS (SELECT 1 FROM creature x WHERE x.id = {c.Id});";

                yield return "INSERT INTO creature_stat (id, body, mind, spirit, health, armor) " +
                    $"SELECT {c.Id}, 15, 15, 15, {c.Hp}, {c.Armor} FROM creature c WHERE c.id = {c.Id} " +
                    $"AND NOT EXISTS (SELECT 1 FROM creature_stat x WHERE x.id = {c.Id});";
            }

            foreach (var (creature, look) in Looks)
                foreach (var sql in Change(creature, new Slot[0], look))
                    yield return sql;

            foreach (var (creature, package, comment) in Packages)
                yield return $"INSERT INTO npc_package (id, package_id, comment) SELECT {creature}, {package}, {S(comment)} " +
                    $"FROM creature c WHERE c.id = {creature} AND NOT EXISTS (SELECT 1 FROM npc_package x WHERE x.id = {creature});";

            foreach (var p in Pools)
                yield return SpawnPool(p.Id, p.Map, p.X, p.Y, p.Z, p.Rotation, p.RespawnTenths, p.Radius, p.Slots);

            foreach (var sql in MissionRows())
                yield return sql;
        }

        public static IEnumerable<string> Down()
        {
            foreach (var sql in InstanceMissionSql.Remove(Revision, Missions))
                yield return sql;

            foreach (var p in Pools)
                yield return $"DELETE FROM spawnpool WHERE id = {p.Id} AND creature_1_Id = {p.Slots[0].Creature};";

            foreach (var (creature, package, _) in Packages)
                yield return $"DELETE FROM npc_package WHERE id = {creature} AND package_id = {package};";

            yield return $"DELETE FROM creature_appearance WHERE id IN ({string.Join(", ", Looks.Select(l => l.Creature))});";

            foreach (var c in Creatures)
            {
                yield return $"DELETE FROM creature_stat WHERE id = {c.Id};";
                yield return $"DELETE FROM creature WHERE id = {c.Id} AND name_id = {c.NameId} " +
                    $"AND NOT EXISTS (SELECT 1 FROM spawnpool p WHERE {c.Id} IN (p.creature_1_Id, p.creature_2_Id, p.creature_3_Id, p.creature_4_Id, p.creature_5_Id, p.creature_6_Id));";
            }

            foreach (var a in Actions)
                yield return $"DELETE FROM creature_action WHERE id = {a.Id};";
        }

        private static string SpawnPool(uint id, uint map, double x, double y, double z, double rotation, uint respawn, double radius,
            params (uint Creature, uint Min, uint Max)[] creatures)
        {
            var slots = creatures.Concat(Enumerable.Repeat((Creature: 0U, Min: 0U, Max: 0U), 6)).Take(6).ToArray();
            var there = string.Concat(creatures.Skip(1).Select(s => $" AND EXISTS (SELECT 1 FROM creature x WHERE x.id = {s.Creature})"));

            return "INSERT INTO spawnpool (id, mode, anim_type, respown_time, pos_x, pos_y, pos_z, rotation, map_context_id, " +
                "creature_1_Id, creature_1_min_count, creature_1_max_count, creature_2_Id, creature_2_min_count, creature_2_max_count, " +
                "creature_3_Id, creature_3_min_count, creature_3_max_count, creature_4_Id, creature_4_min_count, creature_4_max_count, " +
                "creature_5_Id, creature_5_min_count, creature_5_max_count, creature_6_Id, creature_6_min_count, creature_6_max_count, radius) " +
                $"SELECT {id}, 0, 0, {respawn}, {N(x)}, {N(y)}, {N(z)}, {N(rotation)}, {map}, " +
                string.Join(", ", slots.Select(s => $"{s.Creature}, {s.Min}, {s.Max}")) + $", {N(radius)} " +
                $"FROM creature c WHERE c.id = {creatures[0].Creature}{there} AND NOT EXISTS (SELECT 1 FROM spawnpool x WHERE x.id = {id});";
        }

        #endregion

        #region Missions

        private const string Guide = "user-supplied: Ten Ton Hammer, Treeback Camp walkthrough";
        private const string Wiki = "user-supplied: Tabula Rasa wiki, Treeback Camp (overview, missions, Gattsuru's walkthrough, known bugs)";

        // Ten Ton Hammer's credits where it gives them (Treeback Beachhead, Retake the Forward Base, Treeback Field Report);
        // ours, in the same steps, elsewhere. Experience ours, between Warnet Caverns' and the Devil's Den's at these levels.
        public const uint ReportExperience = 12000, Experience = 24000, LeadersExperience = 32000, FinalExperience = 36000;

        // The rewards to choose from: the client's own Treeback armour (MisTreeback*Reward), a slot a mission, each of
        // its four kinds; and Ten Ton Hammer's Class III grenades, bombs and med packs.
        public static readonly uint[] TreebackBoots = { 42471, 42470, 42472, 42469 };       // heavy, mechanized, bio, stealth
        public static readonly uint[] TreebackGloves = { 42463, 42462, 42464, 42461 };
        public static readonly uint[] TreebackHelmets = { 42455, 42454, 42456, 42453 };
        public static readonly uint[] TreebackJackets = { 42459, 42458, 42460, 42457 };
        public static readonly uint[] TreebackLeggings = { 42467, 42466, 42468, 42465 };
        public const uint EmpBomb = 111239, FragmentationGrenade = 45442, StandardMedPack = 45049, ConcussionGrenade = 111014;

        private static IEnumerable<string> MissionRows()
        {
            var sql = new List<string>();
            TreebackBeachheadRows(sql);
            RetakeTheForwardBaseRows(sql);
            TreebackResearchRows(sql);
            OpenTheGateRows(sql);
            CheckTheRidgeRows(sql);
            HowlingRevengeRows(sql);
            CutOffTheHeadRows(sql);
            FinishingMoveRows(sql);
            SabotageRows(sql);
            TreebackGenocideRows(sql);
            MedicalAlertBadgeRows(sql);
            BottleOfTheGoodStuffRows(sql);
            TreebackFieldReportRows(sql);
            return sql;
        }

        /// <summary>Colonel Almos's: report to Bagby at the beachhead.</summary>
        private static void TreebackBeachheadRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, TreebackBeachhead, "Treeback Beachhead", 1504, Almos, Bagby, 21, Category);
            mission.Reward(ReportExperience, 1700, selectableItems: Choice(EmpBomb, FragmentationGrenade, StandardMedPack));
            var scene = Scene(events: false);

            mission.Objective(ReportToBagby, 1512, 9254, 1, MissionObjectiveState.Incomplete);
            mission.Transition(ReportToBagby);
            mission.Conversation(ReportToBagby, BagbyPackage);
            mission.Action(ReportToBagby, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, ReportToBagby, EntranceMarker, CampMouth, Palisades, 15);
            Marker(mission, scene, ReportToBagby, Unnamed(ReportToBagby), BagbyAt, Camp, 8);
            Evidence(mission, "Colonel Almos (name2965) at Viands Village (Ten Ton Hammer's 'Treeback Village', -290, 178, -544) gives it; " +
                "Field Lieutenant Bagby (package46: 'Ten Hut!') at the beachhead by the medic camp inside the Treeback Camp takes it. " +
                "Ten Ton Hammer: 1700 credits and a Class III EMP Bomb, Fragmentation Grenade or Standard Med Pack.");
            mission.Enable(scene);
        }

        /// <summary>Bagby's: the forward base's computer on, then wait for him there.</summary>
        private static void RetakeTheForwardBaseRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, RetakeTheForwardBase, "Retake the Forward Base", 4497, Bagby, Bagby, 21, Category);
            mission.Reward(Experience, 1700, selectableItems: Choice(StandardMedPack, ConcussionGrenade, EmpBomb));
            mission.Prerequisite(TreebackBeachhead);
            var scene = Scene();

            mission.Objective(TurnOnTheComputer, 4501, 8173, 1, MissionObjectiveState.Incomplete);
            mission.Objective(WaitForBagby, 4502, 8174, 2);
            mission.Transition(TurnOnTheComputer);
            mission.Progress(TurnOnTheComputer, MissionProgressEventKind.ScenarioEvent, ComputerOn, counter: Scenario);
            mission.RevealAndActivate(TurnOnTheComputer, WaitForBagby);
            mission.Transition(WaitForBagby);
            mission.Progress(WaitForBagby, MissionProgressEventKind.ScenarioEvent, BagbyArrived, counter: Scenario);
            mission.Action(WaitForBagby, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The forward base's computer, and Bagby at it", ComputerOn, BagbyArrived);
            Marker(mission, scene, TurnOnTheComputer, ComputerMarker, ComputerAt, Camp, 4);
            Marker(mission, scene, WaitForBagby, Unnamed(WaitForBagby), BagbyAtForwardBase, Camp, 10);
            Evidence(mission, "Bagby gives and takes it. The destroyed forward base (its corpses, cots and fallen holosign) west of the " +
                "beachhead; its computer terminal at Ten Ton Hammer's -398.4, 105.7, 266.3 (UsableTwoStateHumComputerV01V06), which " +
                "answers once no Bane stands within 20 m of it ('take out those Thrax and turn the computer back on'); turned on, it " +
                "is objective1 for everyone in the copy on it, and Bagby comes up to it a little later: objective2 for everyone on " +
                "it within 25 m of him. Ten Ton Hammer: 1700 credits and a Class III Standard Med Pack, Concussion Grenade or EMP Bomb.");
            mission.Enable(scene);
        }

        /// <summary>Bagby's: Louden at the front line.</summary>
        private static void TreebackResearchRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, TreebackResearch, "Treeback Research", 4490, Bagby, Louden, 21, Category);
            mission.Reward(ReportExperience, 1700, selectableItems: Choice(TreebackBoots));
            mission.Prerequisite(RetakeTheForwardBase);
            var scene = Scene(events: false);

            mission.Objective(SpeakWithLouden, 5591, 8166, 1, MissionObjectiveState.Incomplete);
            mission.Transition(SpeakWithLouden);
            mission.Conversation(SpeakWithLouden, LoudenPackage);
            mission.Action(SpeakWithLouden, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, SpeakWithLouden, Unnamed(SpeakWithLouden), LoudenAt, Camp, 8);
            Evidence(mission, "Bagby gives it once the forward base is back; Sergeant Louden (package571: 'Bagby sent you?') at the " +
                "valley's mouth before the Bane's gate takes it, Rangers Zamir and Oleev with him. Ten Ton Hammer puts Louden about " +
                "75 m east of Bagby. The client's own Treeback boots (MisTreeback*BootsReward), one of four.");
            mission.Enable(scene);
        }

        /// <summary>Louden's: the force field controller in the south-west pillbox.</summary>
        private static void OpenTheGateRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, OpenTheGate, "Open the Gate", 4503, Louden, Louden, 22, Category);
            mission.Reward(Experience, 1800, selectableItems: Choice(TreebackGloves));
            mission.Prerequisite(TreebackResearch);
            var scene = Scene();

            mission.Objective(ShutOffTheField, 4507, 8171, 1, MissionObjectiveState.Incomplete);
            mission.Transition(ShutOffTheField);
            mission.Progress(ShutOffTheField, MissionProgressEventKind.ScenarioEvent, GateOpen, counter: Scenario);
            mission.Action(ShutOffTheField, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The gate's force field shut off", GateOpen);
            Marker(mission, scene, ShutOffTheField, ControllerMarker, GateSwitchAt, Camp, 4);
            Evidence(mission, "Louden gives and takes it ('to the south is a switchback that leads to a Bane bunker'). The Force Field " +
                "Controller (usable name241 on UsableTwoStateBaneConsoleSwitchV01) by the consoles of the south-west pillbox, Ten Ton " +
                "Hammer's -206.8, 116.4, 50.6; anyone in the copy may throw it, and the field across the gate in the west wall " +
                "(x -118, z 127-133, a Bane minor-entrance field) is down for good in the copy: objective1 for everyone on it. The " +
                "wiki's bug, no marker on the switch, is put right: the client's Force Field Controller marker338.");
            mission.Enable(scene);
        }

        /// <summary>Louden's: the signal from the north bunker, and the dying commando at it.</summary>
        private static void CheckTheRidgeRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, CheckTheRidge, "Check the Ridge", 4515, Louden, Louden, 22, Category);
            mission.Reward(Experience, 1800, selectableItems: Choice(TreebackHelmets));
            mission.Prerequisite(OpenTheGate);
            var scene = Scene();

            mission.Objective(FindTheSignal, 4519, 8158, 1, MissionObjectiveState.Incomplete);
            mission.Objective(TalkToTheCommando, 4521, 8159, 2);
            mission.Transition(FindTheSignal);
            mission.Progress(FindTheSignal, MissionProgressEventKind.ScenarioEvent, SignalFound, counter: Scenario);
            mission.RevealAndActivate(FindTheSignal, TalkToTheCommando);
            mission.Transition(TalkToTheCommando);
            mission.Progress(TalkToTheCommando, MissionProgressEventKind.ScenarioEvent, CommandoFound, counter: Scenario);
            mission.Action(TalkToTheCommando, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The signal, and the dying commando", SignalFound, CommandoFound);
            Marker(mission, scene, FindTheSignal, Unnamed(FindTheSignal), KeenAt, Camp, 30);
            Marker(mission, scene, TalkToTheCommando, Unnamed(TalkToTheCommando), KeenAt, Camp, 5);
            Evidence(mission, "Louden gives and takes it ('You can't deny a man's dying wish'). The distress beacon is Squad Leader " +
                "Keen's (name6721), the commando squad's, at the north bunker, Ten Ton Hammer's -126.7, 120.3, 217.0: within 30 m of " +
                "him is objective1, within 5 m objective2 (the client has no conversation for it). He gives Howling Revenge.");
            mission.Enable(scene);
        }

        /// <summary>The dying commando's: eight howlers of any kind.</summary>
        private static void HowlingRevengeRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, HowlingRevenge, "Howling Revenge", 4543, Keen, Keen, 22, Category);
            mission.Reward(Experience, 1900);
            mission.Prerequisite(OpenTheGate);
            var scene = Scene(events: false);

            mission.Objective(KillAlphaHowlers, 4547, 7557, 1, MissionObjectiveState.Incomplete, counterTextId: 4548);
            mission.Transition(KillAlphaHowlers);
            mission.Progress(KillAlphaHowlers, MissionProgressEventKind.CreatureFlagKilled, HowlerSpecies, counter: 0, target: HowlersWanted);
            mission.Action(KillAlphaHowlers, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, KillAlphaHowlers, Unnamed(KillAlphaHowlers), Kennels, Camp, 40);
            Evidence(mission, "Squad Leader Keen, dying at the north bunker, gives and takes it ('My team was sent in to take out the " +
                "Howlers in the Kennels'). Eight howlers (counter4548 'Alpha Howlers Killed'): the wiki's bug, that only one kind " +
                "counted, is put right by counting the species (SPECIES_HOWLER80) - the kennels' Alpha Beast Howlers, the hunters' " +
                "pets and any other howler - a kill by an AFS NPC is the last player's to hit it, as anywhere. The kennels are the " +
                "barracks east of the gate (48-80, 108.7, 89-124).");
            mission.Enable(scene);
        }

        /// <summary>Galloway's: the three lieutenants, in any order; radioed in.</summary>
        private static void CutOffTheHeadRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, CutOffTheHead, "Cut Off The Head...", 4522, Galloway, null, 23, Category);
            mission.Reward(LeadersExperience, 2200, selectableItems: Choice(TreebackLeggings));
            mission.Prerequisite(OpenTheGate);
            mission.RadioTurnIn();
            var scene = Scene();

            mission.Objective(KillResearcher, 4526, 8161, 1, MissionObjectiveState.Incomplete);
            mission.Objective(KillHunterLord, 4527, 8162, 2, MissionObjectiveState.Incomplete);
            mission.Objective(KillSecurityChief, 4528, 8163, 3, MissionObjectiveState.Incomplete);
            mission.Transition(KillResearcher);
            mission.Progress(KillResearcher, MissionProgressEventKind.ScenarioEvent, ResearcherDead, counter: Scenario);
            mission.Action(KillResearcher, 1, MissionActionKind.GrantReward, reward: 1);
            mission.Transition(KillHunterLord);
            mission.Progress(KillHunterLord, MissionProgressEventKind.ScenarioEvent, HunterLordDead, counter: Scenario);
            mission.Transition(KillSecurityChief);
            mission.Progress(KillSecurityChief, MissionProgressEventKind.ScenarioEvent, SecurityChiefDead, counter: Scenario);

            mission.Scenario(Scenario, "The three lieutenants dead in the copy", ResearcherDead, HunterLordDead, SecurityChiefDead);
            Marker(mission, scene, KillResearcher, Unnamed(KillResearcher), AmuaggorAt, Camp, 15);
            Marker(mission, scene, KillHunterLord, Unnamed(KillHunterLord), CromakAt, Camp, 20);
            Marker(mission, scene, KillSecurityChief, Unnamed(KillSecurityChief), PanelAt, Camp, 6);
            Evidence(mission, "Lieutenant Galloway at the gate gives it; radioed in. Researcher Amuaggor (name6702) in the Treeback " +
                "Pens with a large escort (Ten Ton Hammer's 114, 122, 392), Hunter Lord Cromak (6703) with his hunters east of the gate " +
                "(-26, 104, 144), Security Chief Thraggorth (6704), lured out by the generator station's control panel (usable name119 " +
                "on the station's console pedestal), who comes at 277, 142, 290. The wiki's bugs: all three are open at once, in any " +
                "order; and each one's death in the copy is his objective for everyone on it, whenever they take it up. The client's " +
                "objective5, 'Kill the Bane Leaders', Galloway's reminder (package565), is left out.");
            mission.Enable(scene);
        }

        /// <summary>Offered by radio after Cut Off The Head: Tagak in his command post; radioed in.</summary>
        private static void FinishingMoveRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, FinishingMove, "Finishing Move", 5616, null, null, 24, Category, radioCompleteable: true);
            mission.Reward(FinalExperience, 2400);
            mission.Prerequisite(CutOffTheHead);
            var scene = Scene();

            mission.Objective(KillTheCommander, 5620, 8168, 1, MissionObjectiveState.Incomplete);
            mission.Transition(KillTheCommander);
            mission.Progress(KillTheCommander, MissionProgressEventKind.ScenarioEvent, CommanderDead, counter: Scenario);
            mission.Action(KillTheCommander, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "Base Commander Tagak dead in the copy", CommanderDead);
            Marker(mission, scene, KillTheCommander, Unnamed(KillTheCommander), TagakAt, Camp, 15);
            mission.Channels(MissionChannel.Radio, MissionChannel.Radio, MissionOfferSourceDefinition.MissionRewarded(CutOffTheHead),
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, MissionOfferSourceDefinition.MapArrivalKey, Camp));
            Evidence(mission, "Galloway's, by radio once Cut Off The Head is turned in, and on coming into the camp ('He's here! The " +
                "Bane commander has arrived'); radioed in. Base Commander Tagak (name6701) comes to his command post, the pillbox of " +
                "the south-east (Ten Ton Hammer's 222.5, 128.8, -179.1), once the three lieutenants are dead in the copy; his death in " +
                "it is objective4 for everyone on it. The client's objective5, the reminder, is left out.");
            mission.Enable(scene);
        }

        /// <summary>Kendrick's: the five conduits along the ridge, one after another; radioed in.</summary>
        private static void SabotageRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, Sabotage, "Sabotage", 4549, Kendrick, null, 23, Category);
            mission.Reward(LeadersExperience, 2200, selectableItems: Choice(TreebackJackets));
            mission.Prerequisite(OpenTheGate);
            mission.RadioTurnIn();
            var scene = Scene();

            mission.Objective(AllConduits, 4558, 8181, 1, MissionObjectiveState.Incomplete, counterTextId: 4559);
            mission.Transition(AllConduits);
            for (var i = 0; i < Conduits.Length; i++)
                mission.Progress(AllConduits, MissionProgressEventKind.ScenarioEvent, (uint)i + 1, counter: Scenario,
                    target: (uint)Conduits.Length, trigger: (uint)i + 1);
            mission.Action(AllConduits, 1, MissionActionKind.GrantReward, reward: 1);

            for (var i = 0; i < Conduits.Length; i++)
                mission.Objective(Conduits[i].Objective, Conduits[i].Name, Conduits[i].Body, (uint)i + 2,
                    i == 0 ? MissionObjectiveState.Incomplete : MissionObjectiveState.Inactive);

            for (var i = 0; i < Conduits.Length; i++)
            {
                var c = Conduits[i];
                mission.Transition(c.Objective);
                mission.Progress(c.Objective, MissionProgressEventKind.ScenarioEvent, (uint)i + 1, counter: Scenario);
                if (i + 1 < Conduits.Length)
                    mission.RevealAndActivate(c.Objective, Conduits[i + 1].Objective);
                Marker(mission, scene, c.Objective, ConduitMarker, c.At, Camp, 6);
            }

            mission.Scenario(Scenario, "The power conduits destroyed", Enumerable.Range(1, Conduits.Length).Select(e => (uint)e).ToArray());
            Evidence(mission, "Commander Kendrick, landed inside the gate once it is open, gives it; radioed in ('To the Southeast of " +
                "the last power conduit there's a walled in area filled with Generators'). A Power Conduit (UsableInertDestTreeback" +
                "poweconduitDELETEDUPE) at the foot of each of the ridge's five sonic towers (ArchBaneLargeSonicTowerV01), one after " +
                "the other from the one above the kennels to the one north-west of the generator station, each marked (marker97) " +
                "as it comes; objective9 counts them (counter4559 'Conduits Destroyed'). Each destroyed is its objective for " +
                "everyone on it and stays down in the copy. The client's objective15, a reminder (package46), is left out.");
            mission.Enable(scene);
        }

        /// <summary>Markinson's: NX-22 into the Bane harvester in the pens; then Bagby.</summary>
        private static void TreebackGenocideRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, TreebackGenocide, "Treeback Genocide", 4562, Markinson, Bagby, 24, Category);
            mission.Reward(FinalExperience, 2400);
            mission.Prerequisite(Sabotage);
            var scene = Scene();

            mission.Objective(DeployTheAgent, 4566, 7602, 1, MissionObjectiveState.Incomplete);
            mission.Transition(DeployTheAgent);
            mission.Progress(DeployTheAgent, MissionProgressEventKind.ScenarioEvent, AgentDeployed, counter: Scenario);
            mission.Action(DeployTheAgent, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The NX-22 in the harvester", AgentDeployed);
            Marker(mission, scene, DeployTheAgent, HarvesterMarker, HarvesterAt, Camp, 5);
            scene.Items = new List<MissionItemBinding>
            {
                new("nx-22", NerveGasTemplate, MissionItemScope.AssignmentIssued, 1,
                    MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove)
            };
            scene.AcceptanceItems = new List<CharacterIntent>
            {
                new IssueMissionItemIntent("markinson-nx-22", TreebackGenocide, "nx-22", NerveGasTemplate, 1)
            };
            Evidence(mission, "Captain Markinson, landed in the generator station once Sabotage or Finishing Move is done and no Bane " +
                "stands where he lands, gives it with a canister of NX-22 (MisPalisadesTreebackCalmingAgent, template2320); Bagby " +
                "takes it. The Bane gas harvester (UsableItemAccBaneGasHarvesterV01, marker106 'Bane Harvester') in the Treeback Pens " +
                "takes the canister from anyone on it who holds one: objective5 for everyone on it.");
            mission.Enable(scene);
        }

        /// <summary>Olaera's: Amuaggor's badge.</summary>
        private static void MedicalAlertBadgeRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, MedicalAlertBadge, "Medical Alert Badge", 17865, Olaera, Olaera, 23, Category);
            mission.Reward(Experience, 1900);
            var scene = Scene(events: false);

            mission.Objective(GetTheBadge, 17886, 17887, 1, MissionObjectiveState.Incomplete);
            mission.Transition(GetTheBadge);
            mission.Progress(GetTheBadge, MissionProgressEventKind.ItemAcquired, BadgeClass, target: 1);
            mission.Action(GetTheBadge, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, GetTheBadge, EntranceMarker, CampMouth, Palisades, 15);
            Marker(mission, scene, GetTheBadge, Unnamed(GetTheBadge), AmuaggorAt, Camp, 15);
            scene.Items = new List<MissionItemBinding>
            {
                new("amuaggors-badge", BadgeTemplate, MissionItemScope.CharacterOwned, 1,
                    MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, TurnInQuantity: 1)
            };
            Evidence(mission, "Field Medic Olaera (name10204) at Viands Village gives and takes it. Researcher Amuaggor's Badge " +
                "(MisTreebackMedicalBracelet, template118786) is the Game's to give anyone on it in a copy of the camp where " +
                "Amuaggor has died (TreebackCamp), whether before they took it up or after.");
            mission.Enable(scene);
        }

        /// <summary>Tayros's: the gin to Bagby.</summary>
        private static void BottleOfTheGoodStuffRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, BottleOfTheGoodStuff, "A Bottle of the Good Stuff", 17870, Tayros, Bagby, 21, Category);
            mission.Reward(ReportExperience, 1800, selectableItems: Choice(TreebackBoots[0], TreebackLeggings[0], TreebackBoots[2], TreebackLeggings[2]));
            var scene = Scene(events: false);

            mission.Objective(FindBagby, 17880, 17881, 1, MissionObjectiveState.Incomplete);
            mission.Transition(FindBagby);
            mission.Conversation(FindBagby, BagbyPackage);
            mission.Action(FindBagby, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, FindBagby, EntranceMarker, CampMouth, Palisades, 15);
            Marker(mission, scene, FindBagby, Unnamed(FindBagby), BagbyAt, Camp, 8);
            scene.Items = new List<MissionItemBinding>
            {
                new("liquor", LiquorTemplate, MissionItemScope.AssignmentIssued, 1,
                    MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove)
            };
            scene.AcceptanceItems = new List<CharacterIntent>
            {
                new IssueMissionItemIntent("tayros-liquor", BottleOfTheGoodStuff, "liquor", LiquorTemplate, 1)
            };
            Evidence(mission, "Captain Tayros510122 at Fort Dew gives it with the Liquor for Bagby (MisTreebackBottleOfLiquor, " +
                "template118784); Bagby (package46: 'Well, at least he came through with the liquor') takes it. Ten Ton Hammer: boots " +
                "or legs, here the heavy or bio Treeback ones.");
            mission.Enable(scene);
        }

        /// <summary>Longshanks's: the report to Galloway.</summary>
        private static void TreebackFieldReportRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, TreebackFieldReport, "Treeback Field Report", 17875, Longshanks, Galloway, 21, Category);
            mission.Reward(ReportExperience, 1800, selectableItems: Choice(TreebackJackets));
            var scene = Scene(events: false);

            mission.Objective(DeliverTheReport, 17883, 17884, 1, MissionObjectiveState.Incomplete);
            mission.Transition(DeliverTheReport);
            mission.Conversation(DeliverTheReport, GallowayPackage);
            mission.Action(DeliverTheReport, 1, MissionActionKind.GrantReward, reward: 1);

            Marker(mission, scene, DeliverTheReport, EntranceMarker, CampMouth, Palisades, 15);
            Marker(mission, scene, DeliverTheReport, Unnamed(DeliverTheReport), GallowayAt, Camp, 8);
            scene.Items = new List<MissionItemBinding>
            {
                new("field-report", ReportTemplate, MissionItemScope.AssignmentIssued, 1,
                    MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove)
            };
            scene.AcceptanceItems = new List<CharacterIntent>
            {
                new IssueMissionItemIntent("longshanks-report", TreebackFieldReport, "field-report", ReportTemplate, 1)
            };
            Evidence(mission, "Sergeant Longshanks (name10205) at Viands Village gives it with the Field Report for Galloway " +
                "(MisTreebackFieldReport, template118785); Galloway (package565: 'Did Longshanks send it?') at the gate takes it. " +
                "Ten Ton Hammer: 1800 credits and a vest, here one of the Treeback jackets.");
            mission.Enable(scene);
        }

        public static uint Unnamed(uint objective) => MissionIndicator.UnnamedFrom + objective;

        private static InstanceMissionSql.RewardItem[] Choice(params uint[] templates) =>
            templates.Select(template => new InstanceMissionSql.RewardItem(template, 1)).ToArray();

        private static void Marker(InstanceMissionSql mission, MissionSceneDefinition scene, uint objective, uint id, Vector3 at, uint map, double radius)
        {
            mission.Indicator(objective, id, at.X, at.Y, at.Z, radius);
            scene.IndicatorMaps ??= new Dictionary<uint, Dictionary<uint, uint>>();
            if (!scene.IndicatorMaps.TryGetValue(objective, out var maps))
                scene.IndicatorMaps[objective] = maps = new Dictionary<uint, uint>();
            maps[id] = map;
        }

        private static MissionSceneDefinition Scene(bool events = true) => new()
        {
            Requirement = new CustomRequirement("character.starting-experience-completed"),
            Script = events ? "data.sequence" : null
        };

        private static void Evidence(InstanceMissionSql mission, string reconstruction)
        {
            mission.Evidence(1, MissionEvidenceSourceKind.Client, "trpython:data/generated/client/missionobjective.pyo_dis",
                "Client1.16.5 mission, objective, text and conversation identities.");
            mission.Evidence(2, MissionEvidenceSourceKind.Documentation, Guide, "The walkthrough: people, places and rewards.");
            mission.Evidence(3, MissionEvidenceSourceKind.Documentation, Wiki, "The wiki: the missions, their order, the camp's Bane and its known bugs.");
            mission.Evidence(4, MissionEvidenceSourceKind.Reconstruction, "TreebackCampContent", reconstruction);
        }

        #endregion
    }
}
