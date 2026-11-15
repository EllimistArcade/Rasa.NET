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
    /// The Brann Water Refinery (adv_arieki_torden_plains_brannwaterrefinery, 2093), off the Plains,
    /// as the Tabula Rasa wiki's page and Ten Ton Hammer's walkthrough tell it: the AFS holding on
    /// inside it, the Bane taking it, and its five missions, in the client's own names and texts.
    ///
    /// Who and what:
    ///  - Sergeant Kaliel at the way in, by the medic, who gives We Need Backup!: Field Officer
    ///    Lagda, beside him, is to be talked to and taken to the operations room; Field Officer
    ///    Melokan on the way is to be checked on; Commander Killroy in the operations room takes
    ///    it, with the passkey that opens the locked doors.
    ///  - Captain Jenks below the stairs past the first locked door: Cripple the Defenses (the
    ///    three Bane Hacking Modules, Commander Caolots in Final Processing behind the second
    ///    locked door). Marshall Arboc at the reservoir takes it and gives Defusing the Situation
    ///    (the ten Bane Detonation Devices in the Reservoir Tanks room behind the locked door next
    ///    to him, and in maintenance, each a detonator). Scout Coen at the foot of the skybridge
    ///    takes that and gives Flanks for Nothing: Gunnery Sergeant Namistai behind the locked door
    ///    next to Coen, the skybridge's Bane past the next one, and four sleeping pods
    ///    in each of the two living quarters; radioed in. Hostile Overtaking is offered by radio
    ///    after it: Overseer Ifilion and the Greenhouse's Linker-manned command chairs, then
    ///    Lieutenant Commander Michan at Irendas Colony on the Plains.
    ///  - The Bane at levels 20 to 23: Thrax soldiers, grenadiers and Headhunters, Linkers in the
    ///    Greenhouse, Thrax Cavalrymen on their striders in the reservoir. The bosses are Thrax
    ///    Technicians, with their heal, turret and revive ("all kinds of armor regen powers"):
    ///    Commander Caolots, Researcher Fallorak (between the living quarters) and Executioner
    ///    Kraxor (outside the Greenhouse's north way out) at 25, Overseer Ifilion at his wall screen
    ///    at 28. The two Plains Thrax pools at level 35 in the living
    ///    quarters are the refinery's own Headhunters and soldiers.
    /// Lagda's escort, the two locked doors, the hacking modules, the bombs, the skybridge, the
    /// pods, the Greenhouse's chairs and the scenario events the missions wait for are the
    /// server's, per copy of the refinery (BrannWaterRefinery).
    ///
    /// Health and damage: the Palisades' level 23 Thrax rows' attacks for the soldiers and
    /// grenadiers; the Linkers' and the Cavalrymen's are the Energy Weapon Center's and the Foreas
    /// Base past's halved, a level 8 lower; the bosses' guns Devil's Den's Overlord Gryl's, scaled.
    /// </summary>
    public static class BrannWaterRefineryContent
    {
        public const string Revision = "brann_1_16_5";
        public const uint Refinery = 2093, Plains = 1764;

        /// <summary>missioncategorylanguage: "Instance (Brann Water Refinery)".</summary>
        public const uint Category = 10000031;

        public const uint WeNeedBackup = 1166, CrippleTheDefenses = 1173, DefusingTheSituation = 1174, FlanksForNothing = 1175,
            HostileOvertaking = 1176;

        public static readonly uint[] Missions = { WeNeedBackup, CrippleTheDefenses, DefusingTheSituation, FlanksForNothing, HostileOvertaking };

        // The NPC packages of the missions' conversations (objectiveconversation).
        public const uint LagdaPackage = 1249, MelokanPackage = 1250, NamistaiPackage = 1279;

        // People already in the world: the medic at the way in, and Michan at Irendas Colony.
        public const uint Foxhall = 500124, Michan = 510090;

        // New people and rows.
        public const uint Kaliel = 784001, Lagda = 784002, Melokan = 784003, Killroy = 784004, Jenks = 784005, Arboc = 784006,
            Coen = 784007, Namistai = 784008;
        public const uint Soldier = 784020, Grenadier = 784021, Headhunter = 784022, Cavalryman = 784023, Linker = 784024;
        public const uint Caolots = 784030, Kraxor = 784031, Fallorak = 784032, Ifilion = 784033;

        /// <summary>The refinery's people: Lagda is the copy's (BrannWaterRefinery), the others stand in pools.</summary>
        public static readonly uint[] Afs = { Kaliel, Lagda, Melokan, Killroy, Jenks, Arboc, Coen, Namistai };

        // Items (class, template).
        public const uint DetonatorClass = 10000050, DetonatorTemplate = 50348;   // BWR_Bane_Detonator, "Bane Detonation Device"

        // The objects the Game puts down.
        public const uint ModuleClass = 10191;      // UsableInertDestBaneSecurityMachineryV01
        public const uint BombClass = 20776;        // UsableBombDELETEV01: the detonator's own model (mesh 19000)
        public const uint ChairClass = 23173;       // UsableInertDestBaneCommandChairFloorV01, "Linker Occupied Command Chair"
        public static readonly uint[] PodClasses = { 20621, 20661, 20760, 20661 };   // UsableInertDestBaneSleepPodV01-03

        // usablenameoverridelanguage.
        public const uint ModuleName = 22, BombName = 35, PodName = 36;

        public const uint ModuleHitPoints = 1500, PodHitPoints = 800, ChairHitPoints = 1500;

        // Objectives (missionobjective), and the scenario every mission's events are in.
        public const uint Scenario = 1;
        public const uint EscortLagda = 1, Melokans = 2, AssistLagda = 3;
        public const uint KillCaolots = 2;
        public const uint DefuseAll = 2;
        public const uint ReportToNamistai = 1, Skybridge = 2, NorthernPods = 4, SouthernPods = 5;
        public const uint KillIfilion = 1, Machinery = 2;

        // Events.
        public const uint LagdaDelivered = 1;
        public const uint SkybridgeSecured = 1, NorthernPodsDown = 2, SouthernPodsDown = 3;
        public const uint MachineryDown = 1;

        /// <summary>A Bane Hacking Module: its objective of Cripple the Defenses, its event, and where.</summary>
        public sealed record Module(uint Objective, uint Name, uint Body, uint Event, string Room, Vector3 At, float Yaw);

        public static readonly Module[] Modules =
        {
            new(3, 16280, 16281, 1, "the sediment room's west walk", new Vector3(-29f, 399.6f, -282f), 0f),
            new(4, 16282, 16283, 2, "Final Processing", new Vector3(-90f, 407.6f, -333f), 0f),
            new(5, 16284, 16285, 3, "the ramp down to Marshall Arboc", new Vector3(-20f, 399.7f, -323f), 3.14f)
        };

        /// <summary>A Bane Detonation Device: its objective of Defusing the Situation (its event), and where.</summary>
        public sealed record Bomb(uint Objective, uint Name, uint Body, string Where, Vector3 At);

        public static readonly Bomb[] Bombs =
        {
            new(3, 14773, 14774, "the reservoir, by the east tank", new Vector3(-53f, 383.74f, -350f)),
            new(4, 14775, 14776, "the reservoir, by the south-east tank", new Vector3(-55f, 383.98f, -381f)),
            new(5, 14777, 14778, "the reservoir, by the west tank", new Vector3(-109f, 385.36f, -352f)),
            new(6, 14779, 14780, "the reservoir, by the south-west tank", new Vector3(-104f, 384.73f, -404f)),
            new(7, 14781, 14782, "the east tank's pipes", new Vector3(-57.2f, 387.6f, -358.3f)),
            new(8, 14783, 14784, "the south-east tank's pipes", new Vector3(-71f, 388.6f, -389.4f)),
            new(9, 14785, 14786, "the west tank's pipes", new Vector3(-95f, 387.1f, -369.4f)),
            new(10, 14787, 14788, "the south-west tank's pipes", new Vector3(-106f, 387.6f, -394.2f)),
            new(11, 14789, 14790, "the maintenance room", new Vector3(-147f, 391.6f, -309f)),
            new(12, 14791, 14792, "the way up to the skybridge", new Vector3(-117f, 391.6f, -268f))
        };

        /// <summary>The sleeping pods: the four empty places among each living quarters' six (ArchBaneSleepPodV04).</summary>
        public sealed record Pod(bool North, Vector3 At, float Yaw);

        private const float East = 1.5708f, West = -1.5708f;

        public static readonly Pod[] Pods =
        {
            new(true, new Vector3(-187f, 407.6f, -168.5f), East), new(true, new Vector3(-187f, 407.6f, -172.5f), East),
            new(true, new Vector3(-201f, 407.6f, -168.5f), West), new(true, new Vector3(-201f, 407.6f, -176.5f), West),
            new(false, new Vector3(-187f, 407.6f, -305.5f), East), new(false, new Vector3(-187f, 407.6f, -313.5f), East),
            new(false, new Vector3(-201f, 407.6f, -309.5f), West), new(false, new Vector3(-201f, 407.6f, -313.5f), West)
        };

        /// <summary>The Greenhouse's command chairs, one inside each ring of Bane consoles.</summary>
        public static readonly (Vector3 At, float Yaw)[] Chairs =
        {
            (new Vector3(-235.5f, 398.6f, -238f), 0.79f), (new Vector3(-235.5f, 398.6f, -244f), 2.36f),
            (new Vector3(-239.3f, 398.6f, -238.2f), -0.79f), (new Vector3(-240.5f, 398.8f, -244f), -2.36f)
        };

        /// <summary>
        /// A locked door: the jamb it closes (ArchBrannCorridorJamb014mV01, its middle: 2 m into it),
        /// opened by the passkey Killroy gives with We Need Backup!. The walkthrough's: the first past
        /// the operations room, Final Processing, the one next to Arboc, the one next to Coen (to
        /// Namistai) and the skybridge's way in.
        /// </summary>
        public sealed record Door(string Name, Vector3 Jamb, float Yaw)
        {
            /// <summary>Where the door stands: the jamb's middle, 2 m along its own -Z.</summary>
            public Vector3 At => Jamb + Vector3.Transform(new Vector3(0, 0, -2f), Quaternion.CreateFromYawPitchRoll(Yaw, 0f, 0f));
        }

        public static readonly Door[] Doors =
        {
            new("the way to Captain Jenks", new Vector3(-78f, 415f, -201f), 3.1415927f),
            new("Final Processing", new Vector3(-86f, 407f, -309f), 3.1415927f),
            new("the Reservoir Tanks room", new Vector3(-34f, 383f, -345f), -1.5707964f),
            new("Gunnery Sergeant Namistai", new Vector3(-98f, 399f, -205f), 0f),
            new("the skybridge", new Vector3(-114f, 399f, -233f), 3.1415927f)
        };

        /// <summary>The way in, from the Plains (map_link 76), and where a player arrives.</summary>
        public static readonly Vector3 Arrival = new(-34.0558f, 423.6f, -78.5776f);

        public static readonly Vector3 KalielAt = new(-29.2f, 423.6f, -84.8f);
        public static readonly Vector3 LagdaAt = new(-37.8f, 423.6f, -82.3f);
        public static readonly Vector3 MelokanAt = new(-76f, 415.6f, -135f);
        public static readonly Vector3 KillroyAt = new(-98f, 407.8f, -158f);
        public static readonly Vector3 LagdaAtOperations = new(-96.4f, 407.8f, -152f);
        public static readonly Vector3 JenksAt = new(2f, 407.6f, -185f);
        public static readonly Vector3 ArbocAt = new(-11f, 383.6f, -340f);
        public static readonly Vector3 CoenAt = new(-100f, 399.6f, -217f);
        public static readonly Vector3 NamistaiAt = new(-115.5f, 399.6f, -226f);
        public static readonly Vector3 CaolotsAt = new(-85.3f, 407.6f, -325f);
        public static readonly Vector3 FalloraksAt = new(-194f, 407.6f, -240f);
        public static readonly Vector3 KraxorAt = new(-238f, 399.6f, -205f);
        public static readonly Vector3 IfilionAt = new(-237f, 399.67f, -284f);
        public static readonly Vector3 Reservoir = new(-80f, 383.6f, -375f);
        public static readonly Vector3 SkybridgeMiddle = new(-160f, 399.8f, -241f);
        public static readonly Vector3 NorthernQuarters = new(-194f, 407.6f, -172f);
        public static readonly Vector3 SouthernQuarters = new(-194f, 407.6f, -310f);
        public static readonly Vector3 Greenhouse = new(-238f, 398.8f, -241f);

        public enum TargetCategoryRow : uint { Hostile = 0, Friendly = 1 }

        public sealed record Beast(uint Id, string Comment, uint ClassId, TargetCategoryRow Faction, uint Level, uint Hp,
            uint Armor, uint NameId, uint RunSpeed, uint WalkSpeed, params uint[] Actions);

        public sealed record Act(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint Windup, uint MinDamage, uint MaxDamage, uint DamageType);

        public sealed record Spot(uint Id, double X, double Y, double Z, double Rotation, uint RespawnTenths, double Radius,
            params (uint Creature, uint Min, uint Max)[] Slots);

        private const uint ThraxPistolClass = 20757, GrenadierClass = 9354, StriderClass = 9804, LinkerClass = 9751,
            TechnicianBossClass = 10502;

        // The base world's attacks: the Palisades' Thrax (level 23), the Thrax technicians' heal, turret and revive, the Linkers' channel.
        private const uint SoldierKick = 53097, SoldierPistol = 53098, GrenadierLob = 53099, GrenadierRifle = 53100,
            TechnicianHeal = 62045, TechnicianTurret = 65014, TechnicianRevive = 68079, LinkerChannel = 66001;

        public const uint StriderGun = 784101, StriderBeam = 784102, LinkerChest = 784103, LinkerHand = 784104,
            TechnicianGun = 784105, IfilionsGun = 784106;

        public static readonly Act[] Actions =
        {
            new(StriderGun, "Thrax Cavalryman - Brann Water Refinery strider weapon 1/204", 1, 204, 1, 20, 1000, 0, 105, 158, 1),
            new(StriderBeam, "Thrax Cavalryman - Brann Water Refinery strider_laser_beam", 270, 1, 1, 40, 15000, 2733, 258, 315, 2),
            new(LinkerChest, "Linker - Brann Water Refinery linker_chestblast", 263, 1, 1, 40, 20000, 7333, 153, 229, 1),
            new(LinkerHand, "Linker - Brann Water Refinery linker_hand_blast", 412, 1, 1, 40, 5000, 566, 58, 76, 1),
            new(TechnicianGun, "Brann Water Refinery technician boss 1/296 (level 25)", 1, 296, 1, 20, 800, 0, 100, 150, 5),
            new(IfilionsGun, "Overseer Ifilion 1/296 (level 28)", 1, 296, 1, 20, 800, 0, 140, 210, 5)
        };

        public static readonly Beast[] Creatures =
        {
            new(Kaliel, "Sergeant Kaliel - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 25, 3000, 0, 9118, 0, 0),
            new(Lagda, "Field Officer Lagda - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 9120, 9, 5),
            new(Melokan, "Field Officer Melokan - Brann Water Refinery", SwapsetFemale, TargetCategoryRow.Friendly, 23, 3000, 0, 9121, 0, 0),
            new(Killroy, "Commander Killroy - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 25, 3000, 0, 9119, 0, 0),
            new(Jenks, "Captain Jenks - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 9128, 0, 0),
            new(Arboc, "Marshall Arboc - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 9129, 0, 0),
            new(Coen, "Scout Coen - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 23, 3000, 0, 9131, 0, 0),
            new(Namistai, "Gunnery Sergeant Namistai - Brann Water Refinery", SwapsetMale, TargetCategoryRow.Friendly, 30, 3000, 0, 9132, 0, 0),
            new(Soldier, "Thrax Soldier - Brann Water Refinery", ThraxPistolClass, TargetCategoryRow.Hostile, 21, 1250, 85, 236, 9, 5, SoldierKick, SoldierPistol),
            new(Grenadier, "Thrax Grenadier - Brann Water Refinery", GrenadierClass, TargetCategoryRow.Hostile, 21, 1250, 85, 0, 9, 5, GrenadierLob, GrenadierRifle),
            new(Headhunter, "Thrax Headhunter - Brann Water Refinery", ThraxPistolClass, TargetCategoryRow.Hostile, 22, 1600, 95, 9098, 9, 5, SoldierKick, SoldierPistol),
            new(Cavalryman, "Thrax Cavalryman - Brann Water Refinery", StriderClass, TargetCategoryRow.Hostile, 23, 3000, 120, 9099, 9, 5, StriderGun, StriderBeam),
            new(Linker, "Linker - Brann Water Refinery Greenhouse", LinkerClass, TargetCategoryRow.Hostile, 21, 1250, 85, 412, 9, 5, LinkerChest, LinkerHand, LinkerChannel),
            new(Caolots, "Commander Caolots - Brann Water Refinery", TechnicianBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 9130, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Kraxor, "Executioner Kraxor - Brann Water Refinery", TechnicianBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 9095, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Fallorak, "Researcher Fallorak - Brann Water Refinery", TechnicianBossClass, TargetCategoryRow.Hostile, 25, 7000, 150, 9096, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Ifilion, "Overseer Ifilion - Brann Water Refinery", TechnicianBossClass, TargetCategoryRow.Hostile, 28, 9000, 170, 9097, 9, 5,
                IfilionsGun, TechnicianHeal, TechnicianTurret, TechnicianRevive)
        };

        // Looks: the AFS officers' blue of the seed's officers.
        public const uint FemaleFace = 24009, FemaleHair = 3672;

        private static Slot[] Uniform(uint hair, uint face, uint skin, uint weapon) => new[]
        {
            new Slot(2, OfficerBoots, OfficerBlue), new Slot(13, weapon, 1), new Slot(14, hair, DarkHair),
            new Slot(15, OfficerTunic, OfficerBlue), new Slot(16, OfficerLegs, OfficerBlue), new Slot(17, face, skin)
        };

        public static readonly (uint Creature, Slot[] Look)[] Looks =
        {
            (Kaliel, Uniform(9782, 24032, SkinMedium, RifleAvatar)),
            (Lagda, Uniform(3663, 24020, SkinLight, PistolRedshirt)),
            (Melokan, Uniform(FemaleHair, FemaleFace, SkinMedium, PistolRedshirt)),
            (Killroy, Uniform(9784, 24032, SkinLight, PistolRedshirt)),
            (Jenks, Uniform(3663, 24032, SkinMedium, RifleAvatar)),
            (Arboc, Uniform(9782, 24020, SkinLight, MachineGun)),
            (Coen, Uniform(9784, 24020, SkinMedium, RifleLaser)),
            (Namistai, Uniform(3663, 24032, SkinLight, MachineGun))
        };

        /// <summary>Who speaks which package.</summary>
        public static readonly (uint Creature, uint Package, string Comment)[] Packages =
        {
            (Lagda, LagdaPackage, "Field Officer Lagda - Brann Water Refinery"),
            (Melokan, MelokanPackage, "Field Officer Melokan - Brann Water Refinery"),
            (Namistai, NamistaiPackage, "Gunnery Sergeant Namistai - Brann Water Refinery")
        };

        public const uint NpcRespawn = 200, HostileRespawn = 3000, NamedRespawn = 6000;

        public const uint KalielPool = 784201, MelokanPool = 784202, KillroyPool = 784203, JenksPool = 784204, ArbocPool = 784205,
            CoenPool = 784206, NamistaiPool = 784207;

        /// <summary>The skybridge's pools: all of them dead at once is the skybridge secured (BrannWaterRefinery).</summary>
        public const uint SkybridgeEast = 784225, SkybridgeMiddlePool = 784226, SkybridgeWest = 784227;
        public static readonly uint[] SkybridgePools = { SkybridgeEast, SkybridgeMiddlePool, SkybridgeWest };

        /// <summary>The Plains Thrax the seed put in the living quarters (level 35): the refinery's own now.</summary>
        public const uint NorthernQuartersPool = 531603, SouthernQuartersPool = 531604, PlainsThrax = 531074, PlainsGrenadier = 531075;

        /// <summary>The refinery's pools, all on map 2093.</summary>
        public static readonly Spot[] Pools =
        {
            // The AFS.
            new(KalielPool, KalielAt.X, KalielAt.Y, KalielAt.Z, 3.1, NpcRespawn, 0, (Kaliel, 1, 1)),
            new(MelokanPool, MelokanAt.X, MelokanAt.Y, MelokanAt.Z, 3.1, NpcRespawn, 0, (Melokan, 1, 1)),
            new(KillroyPool, KillroyAt.X, KillroyAt.Y, KillroyAt.Z, 0, NpcRespawn, 0, (Killroy, 1, 1)),
            new(JenksPool, JenksAt.X, JenksAt.Y, JenksAt.Z, 3.1, NpcRespawn, 0, (Jenks, 1, 1)),
            new(ArbocPool, ArbocAt.X, ArbocAt.Y, ArbocAt.Z, -1.6, NpcRespawn, 0, (Arboc, 1, 1)),
            new(CoenPool, CoenAt.X, CoenAt.Y, CoenAt.Z, 3.1, NpcRespawn, 0, (Coen, 1, 1)),
            new(NamistaiPool, NamistaiAt.X, NamistaiAt.Y, NamistaiAt.Z, -1.6, NpcRespawn, 0, (Namistai, 1, 1)),
            // The Bane at the operations room, below it.
            new(784210, -97, 399.6, -180, 0, HostileRespawn, 2, (Soldier, 2, 2)),
            new(784211, -98, 399.6, -195, 0, HostileRespawn, 2, (Grenadier, 1, 1), (Soldier, 1, 1)),
            // Past the first locked door.
            new(784212, -40.1, 415.6, -206.9, 1.6, HostileRespawn, 2, (Soldier, 2, 2)),
            new(784213, -14, 415.6, -209, 1.6, HostileRespawn, 0, (Headhunter, 1, 1)),
            // The hacking modules' walks.
            new(784214, -33.6, 399.6, -288, 0, HostileRespawn, 1, (Soldier, 2, 2)),
            new(784215, -14, 399.6, -315, 0, HostileRespawn, 2, (Headhunter, 2, 2)),
            // Final Processing and Commander Caolots.
            new(784216, -84, 407.74, -318, 3.1, HostileRespawn, 1, (Soldier, 2, 2)),
            new(784217, CaolotsAt.X, CaolotsAt.Y, CaolotsAt.Z, 3.1, NamedRespawn, 0, (Caolots, 1, 1)),
            // The reservoir.
            new(784218, -80, 383.6, -345, 3.1, HostileRespawn, 2, (Soldier, 2, 2)),
            new(784219, -81.74, 383.6, -372.93, 3.1, HostileRespawn, 2, (Grenadier, 2, 2)),
            new(784220, -82, 383.6, -410, 0, HostileRespawn, 2, (Headhunter, 2, 2)),
            new(784221, -120, 383.67, -385, 1.6, HostileRespawn, 0, (Cavalryman, 1, 1)),
            // The maintenance rooms.
            new(784222, -130, 391.6, -300, 1.6, HostileRespawn, 2, (Soldier, 2, 2)),
            new(784223, -165.09, 391.6, -384.15, 0, HostileRespawn, 2, (Grenadier, 2, 2)),
            new(784224, -105, 391.6, -262, 3.1, HostileRespawn, 0, (Headhunter, 1, 1)),
            // The skybridge.
            new(SkybridgeEast, -146, 399.6, -241.4, 1.6, HostileRespawn, 1, (Headhunter, 2, 2)),
            new(SkybridgeMiddlePool, SkybridgeMiddle.X, SkybridgeMiddle.Y, SkybridgeMiddle.Z, 1.6, HostileRespawn, 1, (Soldier, 2, 2)),
            new(SkybridgeWest, -180, 399.63, -240, 1.6, HostileRespawn, 1, (Grenadier, 1, 1), (Headhunter, 1, 1)),
            // Researcher Fallorak on the way from the north living quarters to the south.
            new(784228, FalloraksAt.X, FalloraksAt.Y, FalloraksAt.Z, 0, NamedRespawn, 0, (Fallorak, 1, 1)),
            // The Greenhouse: Overseer Ifilion at his wall screen and the Linkers at the chairs.
            new(784229, IfilionAt.X, IfilionAt.Y, IfilionAt.Z, 0, NamedRespawn, 0, (Ifilion, 1, 1)),
            new(784230, -230.5, 398.8, -241, -1.6, HostileRespawn, 1, (Linker, 2, 2)),
            new(784231, -246, 398.8, -241, 1.6, HostileRespawn, 1, (Linker, 2, 2)),
            // Executioner Kraxor outside the Greenhouse's north way out, and the Bane by the last crate.
            new(784232, KraxorAt.X, KraxorAt.Y, KraxorAt.Z, 3.1, NamedRespawn, 0, (Kraxor, 1, 1)),
            new(784233, -284, 399.6, -259, 1.6, HostileRespawn, 2, (Soldier, 2, 2))
        };

        #region Up and Down

        public static IEnumerable<string> Up()
        {
            foreach (var a in Actions)
                yield return "INSERT INTO creature_action (id, description, action_id, action_arg_id, range_min, range_max, cooldown, windup, min_damage, max_damage, damage_type) " +
                    $"SELECT {a.Id}, {S(a.Description)}, {a.ActionId}, {a.ArgId}, {N(a.RangeMin)}, {N(a.RangeMax)}, {a.Cooldown}, {a.Windup}, {a.MinDamage}, {a.MaxDamage}, {a.DamageType} " +
                    $"FROM entityclass e WHERE e.id = {ThraxPistolClass} AND NOT EXISTS (SELECT 1 FROM creature_action x WHERE x.id = {a.Id});";

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
                yield return SpawnPool(p.Id, Refinery, p.X, p.Y, p.Z, p.Rotation, p.RespawnTenths, p.Radius, p.Slots);

            // The living quarters' Plains Thrax, level 35, are the refinery's Headhunters and soldiers.
            yield return $"UPDATE spawnpool SET creature_1_Id = {Headhunter}, creature_2_Id = {Soldier} " +
                $"WHERE id IN ({NorthernQuartersPool}, {SouthernQuartersPool}) AND map_context_id = {Refinery} " +
                $"AND creature_1_Id = {PlainsThrax} AND creature_2_Id = {PlainsGrenadier} " +
                $"AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {Headhunter}) AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {Soldier});";

            foreach (var sql in MissionRows())
                yield return sql;
        }

        public static IEnumerable<string> Down()
        {
            foreach (var sql in InstanceMissionSql.Remove(Revision, Missions))
                yield return sql;

            yield return $"UPDATE spawnpool SET creature_1_Id = {PlainsThrax}, creature_2_Id = {PlainsGrenadier} " +
                $"WHERE id IN ({NorthernQuartersPool}, {SouthernQuartersPool}) AND map_context_id = {Refinery} " +
                $"AND creature_1_Id = {Headhunter} AND creature_2_Id = {Soldier};";

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

        private const string Guide = "user-supplied: Ten Ton Hammer, Brann Water Refinery walkthrough, parts 1 and 2";
        private const string Wiki = "user-supplied: Tabula Rasa wiki, Brann Water Refinery";

        // The walkthrough's credits, 4600 for each; experience ours, as Devil's Den's at these levels.
        public const uint Credits = 4600, BackupExperience = 19000, Experience = 40000, OvertakingExperience = 44000;

        // The rewards to choose from, the walkthrough's purple gear: the seed's elite templates 20000050-20000058,
        // made for these three missions, each of the class whose levels hold the level the walkthrough names.
        public const uint BioArmorLegs = 20000050;            // Armor_T3_Bio_V07_ELT_Legs_20_to_24 (level 20)
        public const uint IncendiaryInjectorGun = 20000051;   // Weapon_Avatar_InjectionGun_Fire_ELT_22_to_26 (22)
        public const uint CryogenicNetGun = 20000052;         // Weapon_Avatar_NetGun_Ice_ELT_21_to_25 (21)
        public const uint HazmatArmorVest = 20000053;         // Armor_T2_Hazmat_V06_ELT_Vest_19_to_23 (19)
        public const uint IncendiaryNetGun = 20000054;        // Weapon_Avatar_NetGun_Fire_ELT_21_to_25 (21)
        public const uint PulsePistol = 20000055;             // Weapon_Avatar_Pistol_EMP_ELT_20_to_24 (20)
        public const uint StealthArmorVest = 20000056;        // Armor_T3_Stealth_V07_ELT_Vest_20_to_24 (20)
        public const uint VirulentLeechGun = 20000057;        // Weapon_Avatar_LeechGun_Virulent_ELT_19_to_23 (19)
        public const uint RocketLauncher = 20000058;          // Weapon_Avatar_RocketLauncher_Physical_ELT_19_to_23 (19)

        private static IEnumerable<string> MissionRows()
        {
            var sql = new List<string>();
            WeNeedBackupRows(sql);
            CrippleTheDefensesRows(sql);
            DefusingTheSituationRows(sql);
            FlanksForNothingRows(sql);
            HostileOvertakingRows(sql);
            return sql;
        }

        /// <summary>Kaliel's: Lagda talked to and taken to the operations room, Melokan checked on; Killroy takes it.</summary>
        private static void WeNeedBackupRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, WeNeedBackup, "We Need Backup!", 8569, Kaliel, Killroy, 20, Category);
            mission.Reward(BackupExperience, Credits);
            var scene = Scene();

            mission.Objective(AssistLagda, 8748, 8749, 1, MissionObjectiveState.Incomplete);
            mission.Objective(EscortLagda, 8594, 8595, 2);
            mission.Objective(Melokans, 8596, 8597, 3, MissionObjectiveState.Incomplete);

            mission.Transition(AssistLagda);
            mission.Conversation(AssistLagda, LagdaPackage);
            mission.RevealAndActivate(AssistLagda, EscortLagda);
            mission.Action(AssistLagda, 3, MissionActionKind.GrantReward, reward: 1);

            mission.Transition(EscortLagda);
            mission.Progress(EscortLagda, MissionProgressEventKind.ScenarioEvent, LagdaDelivered, counter: Scenario);

            mission.Transition(Melokans);
            mission.Conversation(Melokans, MelokanPackage);

            mission.Scenario(Scenario, "Lagda at the operations room", LagdaDelivered);
            Marker(mission, scene, AssistLagda, LagdaAt, 6);
            Marker(mission, scene, EscortLagda, KillroyAt, 10);
            Marker(mission, scene, Melokans, MelokanAt, 6);
            Evidence(mission, "Sergeant Kaliel at the way in gives it; Commander Killroy in the operations room takes it, with the passkey " +
                "that opens the locked doors (finishing text 8572). Field Officer Lagda (package1249: objective3, 'check in with Melokan on " +
                "the way') is talked to, then follows a player on it to the operations room; Field Officer Melokan (package1250) is the " +
                "rendezvous. The client's objective4 (no desc) is left out.");
            mission.Enable(scene);
        }

        /// <summary>Jenks': the three hacking modules and Commander Caolots; Arboc takes it.</summary>
        private static void CrippleTheDefensesRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, CrippleTheDefenses, "Cripple the Defenses", 8762, Jenks, Arboc, 21, Category);
            mission.Reward(Experience, Credits, selectableItems: new[] { Item(BioArmorLegs), Item(CryogenicNetGun), Item(IncendiaryInjectorGun) });
            mission.Prerequisite(WeNeedBackup);
            var scene = Scene(events: true);

            foreach (var m in Modules)
            {
                mission.Objective(m.Objective, m.Name, m.Body, m.Objective - 2, MissionObjectiveState.Incomplete);
                mission.Transition(m.Objective);
                mission.Progress(m.Objective, MissionProgressEventKind.ScenarioEvent, m.Event, counter: Scenario);
                Marker(mission, scene, m.Objective, m.At, 6);
            }

            mission.Objective(KillCaolots, 8978, 8979, 4, MissionObjectiveState.Incomplete);
            mission.Transition(KillCaolots);
            mission.Progress(KillCaolots, MissionProgressEventKind.CreatureKilled, Caolots);
            mission.Action(KillCaolots, 1, MissionActionKind.GrantReward, reward: 1);
            Marker(mission, scene, KillCaolots, CaolotsAt, 10);

            mission.Scenario(Scenario, "The Bane Hacking Modules", Modules.Select(m => m.Event).ToArray());
            Evidence(mission, "Captain Jenks below the stairs past the first locked door gives it, once We Need Backup! is done ('you got the " +
                "passkey from Killroy'); Marshall Arboc at the reservoir takes it. Three Bane Hacking Modules (usable name22 on " +
                "UsableInertDestBaneSecurityMachineryV01): the sediment room's west walk, Final Processing behind the second locked " +
                "door, where Commander Caolots is, and the ramp down to Arboc (-20, 399, -323). Each destroyed is its objective for everyone on it, and stays down in the copy.");
            mission.Enable(scene);
        }

        /// <summary>Arboc's: the ten bombs, each a detonator; Coen takes it and the detonators.</summary>
        private static void DefusingTheSituationRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, DefusingTheSituation, "Defusing the Situation", 8774, Arboc, Coen, 21, Category);
            mission.Reward(Experience, Credits, selectableItems: new[] { Item(HazmatArmorVest), Item(IncendiaryNetGun), Item(PulsePistol) });
            mission.Prerequisite(CrippleTheDefenses);
            var scene = Scene(events: true);

            mission.Objective(DefuseAll, 9013, 9014, 1, MissionObjectiveState.Incomplete);
            mission.Transition(DefuseAll);
            mission.Progress(DefuseAll, MissionProgressEventKind.ItemAcquired, DetonatorClass, target: (uint)Bombs.Length);
            mission.Action(DefuseAll, 1, MissionActionKind.GrantReward, reward: 1);
            Marker(mission, scene, DefuseAll, Reservoir, 40);

            foreach (var b in Bombs)
            {
                mission.Objective(b.Objective, b.Name, b.Body, b.Objective - 1, MissionObjectiveState.Incomplete);
                mission.Transition(b.Objective);
                mission.Progress(b.Objective, MissionProgressEventKind.ScenarioEvent, b.Objective, counter: Scenario);
                Marker(mission, scene, b.Objective, b.At, 4);
            }

            mission.Scenario(Scenario, "The Bane Detonation Devices", Bombs.Select(b => b.Objective).ToArray());
            Evidence(mission, "Marshall Arboc gives it, once Cripple the Defenses is done; Scout Coen ('see him for debriefing') takes it " +
                "and the ten detonators ('hold onto them for further research'). Ten Bane Detonation Devices (usable name35 on " +
                "UsableBombDELETEV01, the detonator's model): the four on the Reservoir Tanks room's floor, the maintenance room's and " +
                "the last on the way up to Coen are the walkthrough's; its four 'higher up on the pipes' have no places given, and are " +
                "ours, on the tanks' pipes. Each defused gives every member on it who " +
                "has not defused that one a Bane Detonation Device50348. No timer.");
            scene.Items = new List<MissionItemBinding>
            {
                new("detonators", DetonatorTemplate, MissionItemScope.CharacterOwned, (uint)Bombs.Length,
                    MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain,
                    TurnInQuantity: (uint)Bombs.Length)
            };
            mission.Enable(scene);
        }

        /// <summary>Coen's: Namistai, the skybridge, and the pods of both living quarters; radioed in.</summary>
        private static void FlanksForNothingRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, FlanksForNothing, "Flanks for Nothing", 8779, Coen, null, 22, Category);
            mission.Reward(Experience, Credits, selectableItems: new[] { Item(StealthArmorVest), Item(VirulentLeechGun), Item(RocketLauncher) });
            mission.Prerequisite(DefusingTheSituation);
            mission.RadioTurnIn();
            var scene = Scene(events: true);

            mission.Objective(ReportToNamistai, 9194, 9195, 1, MissionObjectiveState.Incomplete);
            mission.Objective(Skybridge, 9197, 9198, 2);
            mission.Objective(NorthernPods, 9201, 9202, 3);
            mission.Objective(SouthernPods, 9203, 9204, 4);

            mission.Transition(ReportToNamistai);
            mission.Conversation(ReportToNamistai, NamistaiPackage);
            mission.RevealAndActivate(ReportToNamistai, Skybridge);

            mission.Transition(Skybridge);
            mission.Progress(Skybridge, MissionProgressEventKind.ScenarioEvent, SkybridgeSecured, counter: Scenario);
            mission.RevealAndActivate(Skybridge, NorthernPods);
            mission.RevealAndActivate(Skybridge, SouthernPods, 3);

            mission.Transition(NorthernPods);
            mission.Progress(NorthernPods, MissionProgressEventKind.ScenarioEvent, NorthernPodsDown, counter: Scenario);
            mission.Action(NorthernPods, 1, MissionActionKind.GrantReward, reward: 1);
            mission.Transition(SouthernPods);
            mission.Progress(SouthernPods, MissionProgressEventKind.ScenarioEvent, SouthernPodsDown, counter: Scenario);

            mission.Scenario(Scenario, "The skybridge and the living quarters", SkybridgeSecured, NorthernPodsDown, SouthernPodsDown);
            Marker(mission, scene, ReportToNamistai, NamistaiAt, 6);
            Marker(mission, scene, Skybridge, SkybridgeMiddle, 25);
            Marker(mission, scene, NorthernPods, NorthernQuarters, 12);
            Marker(mission, scene, SouthernPods, SouthernQuarters, 12);
            Evidence(mission, "Scout Coen gives it, once Defusing the Situation is done; radioed in (finishing text 8782 'new " +
                "developments'). Gunnery Sergeant Namistai (package1279), past the locked door next to Coen, sends the player through the Bane holding it " +
                "('cut these bastards down one by one'): its three pools all dead at once in the copy is the skybridge secured. Then " +
                "the four sleeping pods (usable name36 on UsableInertDestBaneSleepPodV01-03) in each living quarters, in the four empty " +
                "places among the six ArchBaneSleepPodV04 of each; the four of a quarters destroyed is its objective for everyone on it.");
            mission.Enable(scene);
        }

        /// <summary>Offered by radio: Overseer Ifilion and the Greenhouse's chairs, then Michan at Irendas.</summary>
        private static void HostileOvertakingRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, HostileOvertaking, "Hostile Overtaking", 8784, null, Michan, 23, Category);
            mission.Reward(OvertakingExperience, Credits);
            mission.Prerequisite(FlanksForNothing);
            var scene = Scene(events: true);

            mission.Objective(KillIfilion, 9337, 9338, 1, MissionObjectiveState.Incomplete);
            mission.Objective(Machinery, 9339, 9340, 2, MissionObjectiveState.Incomplete);

            mission.Transition(KillIfilion);
            mission.Progress(KillIfilion, MissionProgressEventKind.CreatureKilled, Ifilion);
            mission.Action(KillIfilion, 1, MissionActionKind.GrantReward, reward: 1);
            mission.Transition(Machinery);
            mission.Progress(Machinery, MissionProgressEventKind.ScenarioEvent, MachineryDown, counter: Scenario);

            mission.Scenario(Scenario, "The Greenhouse's machinery", MachineryDown);
            Marker(mission, scene, KillIfilion, IfilionAt, 15);
            Marker(mission, scene, Machinery, Greenhouse, 15);
            scene.HiddenObjectiveIds = new List<uint> { Machinery };

            mission.Channels(MissionChannel.Radio, MissionChannel.Npc, MissionOfferSourceDefinition.MissionRewarded(FlanksForNothing),
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, MissionOfferSourceDefinition.MapArrivalKey, Refinery));
            Evidence(mission, "Offered by radio once Flanks for Nothing is turned in ('We intercepted a Bane distress signal'), and on " +
                "coming into the refinery; Lieutenant Commander Michan510090 at Irendas Colony on the Plains takes it. Overseer Ifilion " +
                "at his wall screen in the Greenhouse (-237, 399, -284); the Greenhouse's 'computer machinery' are four Linker Occupied Command Chairs " +
                "(UsableInertDestBaneCommandChairFloorV01) among its Bane consoles, all four destroyed the client's hidden objective2. " +
                "The walkthrough's Debuff Modification Schematics are left out: no class of the client's names them.");
            mission.Enable(scene);
        }

        private static InstanceMissionSql.RewardItem Item(uint template) => new(template, 1);

        private static void Marker(InstanceMissionSql mission, MissionSceneDefinition scene, uint objective, Vector3 at, double radius)
        {
            var id = MissionIndicator.UnnamedFrom + objective;
            mission.Indicator(objective, id, at.X, at.Y, at.Z, radius);
            scene.IndicatorMaps ??= new Dictionary<uint, Dictionary<uint, uint>>();
            if (!scene.IndicatorMaps.TryGetValue(objective, out var maps))
                scene.IndicatorMaps[objective] = maps = new Dictionary<uint, uint>();
            maps[id] = Refinery;
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
            mission.Evidence(2, MissionEvidenceSourceKind.Documentation, Guide, "The walkthrough: people, places, order and rewards.");
            mission.Evidence(3, MissionEvidenceSourceKind.Documentation, Wiki, "The wiki: the people's and the Bane's levels.");
            mission.Evidence(4, MissionEvidenceSourceKind.Reconstruction, "BrannWaterRefineryContent", reconstruction);
        }

        #endregion
    }
}
