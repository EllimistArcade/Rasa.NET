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
    /// Guardian Prominence (adv_foreas_concordia_wilderness_guardianprom, 1416), off the Wilderness,
    /// as the Tabula Rasa wiki's page and the user's walkthrough tell it: the Callisto's survivors,
    /// the AFS at the Temple of the Ages, the Bane at levels 38 to 41 and their three commanders, and
    /// its four missions, in the client's own names and texts. And the three level 38 Bane guards
    /// at its entrance in the Wilderness ("the only level 38 Bane in Wilderness").
    ///
    /// Who and what:
    ///  - Researcher Sonia Anand and Ranger Tomnel (a Forean: "managed to drop Annan's daughter and
    ///    the Forean") by the First Aid Station at the way in: The Fate Of The Callisto. The
    ///    Callisto's flight recorder lies by its wreck on the mountain top (-150, 196); Professor
    ///    Anand with the survivors in the cave under the temple's north wall (-55, 303, 79), who
    ///    takes it and gives The Chamber Of Eternity with the Guardian Stone.
    ///  - Lieutenant Danchekker and Communications Officer Cronauer, at his radio, with the AFS at
    ///    the Temple of the Ages: Breaking The Chain Of Command and No Soap Radio.
    ///  - The Bane: Thrax soldiers and grenadiers, Hunters with their Howlers up the mountain, Filcher
    ///    Larcenists in the cave's upper fork, Thrax technicians in the caves to the ruins. The
    ///    commanders: Thrax Taskmaster Horga (40, a rifleman) on Mount Epoch across the bridge,
    ///    Thrax Overseer Lurge (41, a grenadier) at the cave to the Chamber of Eternity, Thrax
    ///    Preceptor Zor (40, an engineer, his turret) at the chamber's door.
    ///  - The five Biomechanical Jamming Towers (usable name 4, on the Bane brain console): two in
    ///    the open field by the temple, one towards the bridge, two on Mount Epoch.
    /// The jamming towers, the chamber's door, the Juggernauts Danchekker's mission brings on the
    /// camp, No Soap Radio's reinforcements and the scenario events the missions wait for are the
    /// server's, per copy of the instance (GuardianProminence).
    ///
    /// None of the client's data places anyone or anything here: the places are the guides' and
    /// the map's (the wreck, the camp's sandbags and radio table, the survivors' stretchers, the
    /// cave mouth, the ruins' cavern and the chamber's door jamb), on the navmesh. Health and damage:
    /// the Incline's, the Ashen Desert's and Thunderhead's Thrax rows' attacks at these levels; the
    /// commanders' and the Juggernauts' ours, scaled from the world's bosses at those levels.
    /// </summary>
    public static class GuardianProminenceContent
    {
        public const string Revision = "guardprom_1_16_5";
        public const uint Prominence = 1416, Wilderness = 1220;

        /// <summary>missioncategorylanguage: "Instance (Guardian Prominence)".</summary>
        public const uint Category = 10000047;

        public const uint FateOfTheCallisto = 706, ChamberOfEternity = 1127, NoSoapRadio = 712, ChainOfCommand = 708;

        public static readonly uint[] Missions = { FateOfTheCallisto, ChamberOfEternity, NoSoapRadio, ChainOfCommand };

        /// <summary>"You cannot obtain any missions in that instance until level 36" (the walkthrough).</summary>
        public const uint MinimumLevel = 36;

        // The NPC packages of the missions' conversations (objectiveconversation): the flight recorder's and Professor Anand's.
        public const uint RecorderPackage = 612, ProfessorPackage = 303;

        /// <summary>The Future shrine in the Chamber of Eternity (logos 58), which The Chamber Of Eternity is done by.</summary>
        public const uint FutureLogos = 58;

        // People.
        public const uint Sonia = 787001, Tomnel = 787002, Professor = 787003, Danchekker = 787004, Cronauer = 787005,
            CampSoldier = 787006, Reinforcement = 787007, ReinforcementMech = 787008;

        // Bane.
        public const uint Soldier = 787020, Grenadier = 787021, Hunter = 787022, EliteSoldier = 787023, Technician = 787024,
            Larcenist = 787025, Juggernaut = 787026;
        public const uint Horga = 787030, Lurge = 787031, Zor = 787032;

        /// <summary>The instance's people: none of them is harmed, and the creatures leave all but the camp's soldiers alone.</summary>
        public static readonly uint[] Afs = { Sonia, Tomnel, Professor, Danchekker, Cronauer, CampSoldier, Reinforcement, ReinforcementMech };

        /// <summary>Who no creature goes looking for: the people who are not soldiers.</summary>
        public static readonly uint[] KeptFromCreatures = { Sonia, Tomnel, Professor, Danchekker, Cronauer };

        // Items (class, template): the Guardian Stone, MisGuardpromItemElohArtifact.
        public const uint GuardianStoneClass = 23999, GuardianStoneTemplate = 49667;

        // What the Game puts down.
        public const uint RecorderClass = 10630;    // MisGuardpromItemCallistoFlightRecorder (prop_hum_recorder_v01)
        public const uint TowerClass = 7502;        // UsableInertDestBaneConsoleBrainBase: the client's own, the NPC
                                                    // MisGuardPromThraxJammingUnit 10645, cannot be a creature or be shot
        public const uint DoorClass = 10605;        // UsableDoorElohV01, in the map's ArchElohDoorJamb

        // usablenameoverridelanguage.
        public const uint TowerName = 4, DoorName = 2;

        public const uint TowerHitPoints = 3000;

        // Objectives (missionobjective), and the scenario every mission's events are in.
        public const uint Scenario = 1;
        public const uint FindRecorder = 1, FindCrew = 2;
        public const uint RetrieveLogos = 1;
        public const uint KillHorga = 1, KillZor = 5, KillLurge = 6;

        /// <summary>A Biomechanical Jamming Tower: its objective of No Soap Radio, its event, and where.</summary>
        public sealed record Tower(uint Objective, uint Name, uint Body, uint Event, string Where, Vector3 At, float Yaw);

        public static readonly Tower[] Towers =
        {
            new(2, 17204, 17205, 1, "the open field by the Temple of the Ages", new Vector3(-62f, 335.08f, 150f), 0.4f),
            new(3, 17206, 17207, 2, "the open field by the Temple of the Ages, to the north", new Vector3(-45f, 335.58f, 200f), 2.6f),
            new(4, 17208, 17209, 3, "the mountain top, on the way back towards the bridge", new Vector3(40f, 351.71f, 110f), -1.2f),
            new(5, 17210, 17211, 4, "Mount Epoch, across the bridge", new Vector3(150f, 345.03f, 20f), 1.9f),
            new(6, 17212, 17213, 5, "Mount Epoch, by the Forean temple", new Vector3(195f, 346.91f, 30f), -2.2f)
        };

        /// <summary>The Chamber of Eternity's door: in the map's ArchElohDoorJamb (328, 332, 256), across the corridor from the ruins.</summary>
        public static readonly Vector3 DoorAt = new(328f, 332f, 256f);
        public const float DoorYaw = -1.5707964f;

        /// <summary>The way in, from the Wilderness (map_link 36), where a player arrives.</summary>
        public static readonly Vector3 Arrival = new(163.4648f, 314.3326f, 200.2778f);

        public static readonly Vector3 SoniaAt = new(155.2f, 316.28f, 192.5f);
        public static readonly Vector3 TomnelAt = new(157.2f, 316.88f, 190f);
        public static readonly Vector3 RecorderAt = new(-150f, 336.39f, 196f);
        public static readonly Vector3 ProfessorAt = new(-56.5f, 303.33f, 79.5f);
        public static readonly Vector3 DanchekkerAt = new(-55f, 340.36f, 30f);
        public static readonly Vector3 CronauerAt = new(-58.45f, 340.88f, 48.18f);
        public static readonly Vector3 Camp = new(-56f, 340.28f, 40f);
        public static readonly Vector3 HorgaAt = new(172f, 344.97f, 40f);
        public static readonly Vector3 LurgeAt = new(48f, 343.82f, 192f);
        public static readonly Vector3 ZorAt = new(318f, 332.53f, 256f);
        public static readonly Vector3 Chamber = new(351.74f, 333f, 256f);

        /// <summary>The camp's soldiers, at its sandbags.</summary>
        public static readonly Vector3[] CampSoldiers =
        {
            new(-78f, 341.01f, 36f), new(-47f, 340.87f, 37f), new(-78f, 340.94f, 2f), new(-33.2f, 340.88f, 2.8f)
        };

        /// <summary>No Soap Radio's reinforcements (GuardianProminence): four soldiers and a mech, on the camp's north side.</summary>
        public static readonly Vector3[] Reinforcements =
        {
            new(-70f, 339.88f, 55f), new(-45f, 340.31f, 55f), new(-63.4f, 341.16f, 60.3f), new(-52f, 340.28f, 60f)
        };
        public static readonly Vector3 MechAt = new(-56f, 340.28f, 52f);

        /// <summary>Where Danchekker's Juggernauts come out of the open field (GuardianProminence), and march on the camp.</summary>
        public static readonly Vector3[] JuggernautsFrom = { new(-53.7f, 338.44f, 215.4f), new(-70f, 335.71f, 200f) };

        /// <summary>The Wilderness entrance (map_link 12 at (-24.3, 254.7, 690.5)) and the three level 38 guards at it.</summary>
        public static readonly Vector3 WildernessEntrance = new(-24.3419f, 254.6502f, 690.5391f);

        public static readonly Vector3[] Guards =
        {
            new(-34f, 252.98f, 686f), new(-28.54f, 253.12f, 685.8f), new(-17.34f, 256.52f, 684.4f)
        };

        public enum TargetCategoryRow : uint { Hostile = 0, Friendly = 1 }

        public sealed record Beast(uint Id, string Comment, uint ClassId, TargetCategoryRow Faction, uint Level, uint Hp,
            uint Armor, uint NameId, uint RunSpeed, uint WalkSpeed, params uint[] Actions);

        public sealed record Act(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint Windup, uint MinDamage, uint MaxDamage, uint DamageType);

        public sealed record Spot(uint Id, uint Map, double X, double Y, double Z, double Rotation, uint RespawnTenths, double Radius,
            params (uint Creature, uint Min, uint Max)[] Slots);

        private const uint ThraxClass = 20757, GrenadierClass = 9354, HunterClass = 10166, TechnicianClass = 7043, FilcherClass = 6421,
            JuggernautClass = 7722, MechClass = 10235, RifleBossClass = 10504, GrenadierBossClass = 21882, TechnicianBossClass = 10502;

        // The world's attacks at these levels: the Incline's Thrax soldier (37), the Ashen Desert's grenadier (39), the Mires'
        // Hunter (36, net, gun and Howler), Thunderhead's soldier (40) and technician (41, heal, turret, revive), the Howling
        // Maw Filcher's theft; and the bosses' Thrax lightning, rage, nanites, tectonic strike, necromite and heal.
        private const uint SoldierKick = 53060, SoldierGun = 53061, GrenadierLob = 53014, GrenadierRifle = 53015,
            HunterNet = 53093, HunterGun = 53094, HunterPet = 65033, EliteKick = 53155, EliteGun = 53156,
            TechnicianGun = 70030, TechnicianHeal = 62049, TechnicianTurret = 65018, TechnicianRevive = 68083, FilcherTheft = 63003,
            ThraxLightning = 57029, ThraxRage = 61045, ExplosiveNanites = 56028, TectonicStrike = 68060, Necromite = 68061,
            BossHeal = 62054;

        public const uint LarcenistClaw = 787101, JuggernautGun = 787102, JuggernautCannon = 787103, JuggernautMortars = 787104,
            AfsRifle = 787105, MechMinigun = 787106, MechMissiles = 787107, HorgasRifle = 787108, HorgasKick = 787109,
            LurgesLob = 787110, LurgesRifle = 787111, ZorsGun = 787112;

        public static readonly Act[] Actions =
        {
            new(LarcenistClaw, "Filcher Larcenist - Guardian Prominence filcher_melee", 207, 1, 0.5, 3, 4000, 1066, 560, 750, 1),
            new(JuggernautGun, "Juggernaut - Guardian Prominence weapon 1/180", 1, 180, 1, 60, 1500, 0, 250, 380, 1),
            new(JuggernautCannon, "Juggernaut - Guardian Prominence cannon 428/1", 428, 1, 1, 40, 8000, 0, 560, 800, 1),
            new(JuggernautMortars, "Juggernaut - Guardian Prominence mortars 244/1", 244, 1, 10, 60, 12000, 0, 500, 750, 1),
            new(AfsRifle, "AFS soldier - Guardian Prominence rifle 1/134", 1, 134, 1, 30, 800, 0, 60, 90, 1),
            new(MechMinigun, "AFS mech - Guardian Prominence minigun 1/224", 1, 224, 1, 25, 400, 0, 40, 60, 1),
            new(MechMissiles, "AFS mech - Guardian Prominence missiles 141/17", 141, 17, 1, 40, 1000, 0, 120, 180, 1),
            new(HorgasRifle, "Thrax Taskmaster Horga 1/79 (level 40)", 1, 79, 0.5, 30, 800, 0, 500, 750, 1),
            new(HorgasKick, "Thrax Taskmaster Horga thrax_kick (level 40)", 397, 3, 0.5, 3, 10000, 333, 170, 260, 1),
            new(LurgesLob, "Thrax Overseer Lurge 1/229 (level 41)", 1, 229, 5, 15, 2500, 0, 380, 570, 2),
            new(LurgesRifle, "Thrax Overseer Lurge 1/79 (level 41)", 1, 79, 1, 15, 800, 0, 380, 570, 1),
            new(ZorsGun, "Thrax Preceptor Zor 1/296 (level 40)", 1, 296, 1, 20, 800, 0, 480, 720, 5)
        };

        public static readonly Beast[] Creatures =
        {
            new(Sonia, "Researcher Anand - Guardian Prominence", SwapsetFemale, TargetCategoryRow.Friendly, 38, 6000, 0, 3155, 0, 0),
            new(Tomnel, "Ranger Tomnel - Guardian Prominence", ForeanSpearman, TargetCategoryRow.Friendly, 38, 6000, 0, 3154, 0, 0),
            new(Professor, "Professor Anand - Guardian Prominence", SwapsetMale, TargetCategoryRow.Friendly, 38, 6000, 0, 3156, 0, 0),
            new(Danchekker, "Lieutenant Danchekker - Guardian Prominence", SwapsetMale, TargetCategoryRow.Friendly, 40, 8000, 0, 6745, 0, 0),
            new(Cronauer, "Communications Officer Cronauer - Guardian Prominence", SwapsetMale, TargetCategoryRow.Friendly, 38, 6000, 0, 6749, 0, 0),
            new(CampSoldier, "AFS soldier - Guardian Prominence camp", Infantryman, TargetCategoryRow.Friendly, 40, 6000, 200, 0, 9, 5, AfsRifle),
            new(Reinforcement, "AFS soldier - Guardian Prominence reinforcements", Infantryman, TargetCategoryRow.Friendly, 40, 6000, 200, 0, 9, 5, AfsRifle),
            new(ReinforcementMech, "AFS mech - Guardian Prominence reinforcements", MechClass, TargetCategoryRow.Friendly, 40, 15000, 300, 0, 9, 5, MechMinigun, MechMissiles),
            new(Soldier, "Thrax Soldier - Guardian Prominence", ThraxClass, TargetCategoryRow.Hostile, 38, 6200, 180, 238, 9, 5, SoldierKick, SoldierGun),
            new(Grenadier, "Thrax Grenadier - Guardian Prominence", GrenadierClass, TargetCategoryRow.Hostile, 39, 5783, 185, 0, 9, 5, GrenadierLob, GrenadierRifle),
            new(Hunter, "Hunter - Guardian Prominence", HunterClass, TargetCategoryRow.Hostile, 38, 4800, 175, 390, 9, 5, HunterNet, HunterGun, HunterPet),
            new(EliteSoldier, "Thrax Soldier - Guardian Prominence caves", ThraxClass, TargetCategoryRow.Hostile, 40, 6307, 190, 355, 9, 5, EliteKick, EliteGun),
            new(Technician, "Thrax Technician - Guardian Prominence", TechnicianClass, TargetCategoryRow.Hostile, 40, 8409, 285, 8064, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret, TechnicianRevive),
            new(Larcenist, "Filcher Larcenist - Guardian Prominence", FilcherClass, TargetCategoryRow.Hostile, 38, 6000, 180, 8011, 9, 5, LarcenistClaw, FilcherTheft),
            new(Juggernaut, "Juggernaut - Guardian Prominence assault", JuggernautClass, TargetCategoryRow.Hostile, 40, 12000, 300, 10757, 9, 5,
                JuggernautGun, JuggernautCannon, JuggernautMortars),
            new(Horga, "Thrax Taskmaster Horga - Guardian Prominence", RifleBossClass, TargetCategoryRow.Hostile, 40, 20000, 420, 6748, 9, 5,
                HorgasRifle, HorgasKick, ThraxLightning, ThraxRage),
            new(Lurge, "Thrax Overseer Lurge - Guardian Prominence", GrenadierBossClass, TargetCategoryRow.Hostile, 41, 24000, 430, 6746, 9, 5,
                LurgesLob, LurgesRifle, ExplosiveNanites, TectonicStrike, Necromite),
            new(Zor, "Thrax Preceptor Zor - Guardian Prominence", TechnicianBossClass, TargetCategoryRow.Hostile, 40, 22000, 420, 6747, 9, 5,
                ZorsGun, BossHeal, TechnicianTurret, TechnicianRevive)
        };

        // Looks.
        public const uint FemaleFace = 24009, FemaleHair = 3672, MaleFace = 24032, OtherMaleFace = 24020;

        private static Slot[] Uniform(uint hair, uint face, uint skin, uint weapon) => new[]
        {
            new Slot(2, OfficerBoots, OfficerBlue), new Slot(13, weapon, 1), new Slot(14, hair, DarkHair),
            new Slot(15, OfficerTunic, OfficerBlue), new Slot(16, OfficerLegs, OfficerBlue), new Slot(17, face, skin)
        };

        /// <summary>The Anands' field whites: the medics' torso, legs and boots.</summary>
        private static Slot[] Whites(uint hair, uint face, uint skin) => new[]
        {
            new Slot(2, MedicBoots, White), new Slot(14, hair, DarkHair), new Slot(15, MedicTorso, White),
            new Slot(16, MedicLegs, White), new Slot(17, face, skin)
        };

        public static readonly (uint Creature, Slot[] Look)[] Looks =
        {
            (Sonia, Whites(FemaleHair, FemaleFace, SkinMedium)),
            (Tomnel, new[] { new Slot(13, ForeanSpear, 1) }),
            (Professor, Whites(9782, OtherMaleFace, SkinMedium)),
            (Danchekker, Uniform(9784, MaleFace, SkinLight, RifleAvatar)),
            (Cronauer, Uniform(3663, OtherMaleFace, SkinLight, PistolRedshirt)),
            (CampSoldier, new[] { new Slot(13, RifleAvatar, 1) }),
            // A Hunter's shield in its weapon slot, as every Hunter has it (HunterShields): unarmed, it never draws it.
            (Hunter, new[] { new Slot(HunterShields.WeaponSlot, HunterShields.Shield, HunterShields.Color) }),
            (Reinforcement, new[] { new Slot(13, RifleAvatar, 1) })
        };

        /// <summary>Who speaks which package.</summary>
        public static readonly (uint Creature, uint Package, string Comment)[] Packages =
        {
            (Professor, ProfessorPackage, "Professor Anand - Guardian Prominence")
        };

        public const uint NpcRespawn = 200, HostileRespawn = 3000, NamedRespawn = 6000;

        public const uint SoniaPool = 787201, TomnelPool = 787202, ProfessorPool = 787203, DanchekkerPool = 787204, CronauerPool = 787205;
        public const uint HorgaPool = 787240, LurgePool = 787241, ZorPool = 787242;
        public const uint GuardsPool = 787250;

        public static readonly Spot[] Pools =
        {
            // The people.
            new(SoniaPool, Prominence, SoniaAt.X, SoniaAt.Y, SoniaAt.Z, -2.4, NpcRespawn, 0, (Sonia, 1, 1)),
            new(TomnelPool, Prominence, TomnelAt.X, TomnelAt.Y, TomnelAt.Z, -2.4, NpcRespawn, 0, (Tomnel, 1, 1)),
            new(ProfessorPool, Prominence, ProfessorAt.X, ProfessorAt.Y, ProfessorAt.Z, 1.6, NpcRespawn, 0, (Professor, 1, 1)),
            new(DanchekkerPool, Prominence, DanchekkerAt.X, DanchekkerAt.Y, DanchekkerAt.Z, 0, NpcRespawn, 0, (Danchekker, 1, 1)),
            new(CronauerPool, Prominence, CronauerAt.X, CronauerAt.Y, CronauerAt.Z, 2.4, NpcRespawn, 0, (Cronauer, 1, 1)),
            new(787206, Prominence, CampSoldiers[0].X, CampSoldiers[0].Y, CampSoldiers[0].Z, 0, NpcRespawn, 0, (CampSoldier, 1, 1)),
            new(787207, Prominence, CampSoldiers[1].X, CampSoldiers[1].Y, CampSoldiers[1].Z, 0, NpcRespawn, 0, (CampSoldier, 1, 1)),
            new(787208, Prominence, CampSoldiers[2].X, CampSoldiers[2].Y, CampSoldiers[2].Z, 3.1, NpcRespawn, 0, (CampSoldier, 1, 1)),
            new(787209, Prominence, CampSoldiers[3].X, CampSoldiers[3].Y, CampSoldiers[3].Z, 3.1, NpcRespawn, 0, (CampSoldier, 1, 1)),
            // Up the mountain from the way in: Hunters and their Howlers.
            new(787210, Prominence, 111.01, 310.57, 75.42, 2.6, HostileRespawn, 2, (Hunter, 1, 1)),
            new(787211, Prominence, 59.2, 315.28, 41.2, 1.6, HostileRespawn, 3, (Hunter, 2, 2)),
            new(787212, Prominence, 40, 333.05, 68, 0, HostileRespawn, 3, (Hunter, 1, 1), (Soldier, 1, 1)),
            // The mountain top.
            new(787213, Prominence, 29.6, 353.68, 124, -1.6, HostileRespawn, 3, (Soldier, 2, 2)),
            new(787214, Prominence, 0, 348.05, 130, -1.6, HostileRespawn, 3, (Grenadier, 1, 1), (Soldier, 1, 1)),
            new(787215, Prominence, 35, 350.79, 105, 0, HostileRespawn, 3, (Soldier, 1, 1), (Grenadier, 1, 1)),
            // Round the Callisto's wreck.
            new(787216, Prominence, -148, 336.51, 215, 3.1, HostileRespawn, 3, (Soldier, 2, 2)),
            new(787217, Prominence, -162, 335.55, 185, 1.6, HostileRespawn, 3, (Grenadier, 1, 1), (Soldier, 1, 1)),
            new(787218, Prominence, -135.81, 335.53, 200.69, -1.6, HostileRespawn, 2, (Hunter, 1, 1)),
            // The cave's upper fork, and its ledge over the survivors: Filcher Larcenists.
            new(787219, Prominence, -110, 326.98, 132, 1.6, HostileRespawn, 3, (Larcenist, 2, 2)),
            new(787220, Prominence, -54, 321.99, 86, 3.1, HostileRespawn, 1, (Larcenist, 1, 1)),
            // The open field by the Temple of the Ages.
            new(787221, Prominence, -72.41, 335.98, 110.59, 3.1, HostileRespawn, 3, (Soldier, 2, 2)),
            new(787222, Prominence, -65, 335.35, 140, 3.1, HostileRespawn, 3, (Soldier, 2, 2)),
            new(787223, Prominence, -48, 335.48, 190, 3.1, HostileRespawn, 3, (Grenadier, 1, 1), (Soldier, 1, 1)),
            // Mount Epoch.
            new(787224, Prominence, 160, 347.45, 10, -0.8, HostileRespawn, 3, (Soldier, 2, 2)),
            new(787225, Prominence, 190, 351.27, 15, -0.8, HostileRespawn, 3, (Grenadier, 1, 1), (Soldier, 1, 1)),
            new(HorgaPool, Prominence, HorgaAt.X, HorgaAt.Y, HorgaAt.Z, -0.8, NamedRespawn, 0, (Horga, 1, 1)),
            // The cave to the Chamber of Eternity, Overseer Lurge at its mouth.
            new(LurgePool, Prominence, LurgeAt.X, LurgeAt.Y, LurgeAt.Z, -1.6, NamedRespawn, 0, (Lurge, 1, 1)),
            new(787226, Prominence, 45, 344.64, 185, -1.6, HostileRespawn, 3, (Soldier, 1, 1), (Grenadier, 1, 1)),
            new(787227, Prominence, 84, 347.11, 192, -1.6, HostileRespawn, 2, (EliteSoldier, 2, 2)),
            new(787228, Prominence, 100, 339.08, 216.8, -2.4, HostileRespawn, 1, (Grenadier, 1, 1)),
            new(787229, Prominence, 116, 331.36, 248, -1.6, HostileRespawn, 2, (EliteSoldier, 2, 2)),
            // The ruins' cavern, to the chamber's door and Preceptor Zor.
            new(787230, Prominence, 140, 331.13, 256, -1.6, HostileRespawn, 2, (Technician, 1, 1), (EliteSoldier, 1, 1)),
            new(787231, Prominence, 200, 325.88, 256, -1.6, HostileRespawn, 3, (EliteSoldier, 2, 2)),
            new(787232, Prominence, 240, 325.88, 256, -1.6, HostileRespawn, 3, (Grenadier, 1, 1), (EliteSoldier, 1, 1)),
            new(787233, Prominence, 270, 329.53, 256, -1.6, HostileRespawn, 1, (Technician, 1, 1)),
            new(787234, Prominence, 302, 335.28, 252.8, -1.6, HostileRespawn, 2, (EliteSoldier, 2, 2)),
            new(ZorPool, Prominence, ZorAt.X, ZorAt.Y, ZorAt.Z, -1.6, NamedRespawn, 0, (Zor, 1, 1)),
            // The Wilderness: the three level 38 guards at the way in.
            new(GuardsPool, Wilderness, Guards[1].X, Guards[1].Y, Guards[1].Z, 3.1, HostileRespawn, 0, (Soldier, 1, 1)),
            new(GuardsPool + 1, Wilderness, Guards[0].X, Guards[0].Y, Guards[0].Z, 3.1, HostileRespawn, 0, (Soldier, 1, 1)),
            new(GuardsPool + 2, Wilderness, Guards[2].X, Guards[2].Y, Guards[2].Z, 3.1, HostileRespawn, 0, (Soldier, 1, 1))
        };

        /// <summary>
        /// The Wilderness gives every kill on it ordinary loot by its creature row (loot_map_policy), and a
        /// row without a binding has none. The guards' row is bound to the Wilderness' highest Thrax profile.
        /// </summary>
        public const string GuardsLootProfile = "wilderness_v1_thrax_15_19_boss";

        #region Up and Down

        public static IEnumerable<string> Up()
        {
            foreach (var a in Actions)
                yield return "INSERT INTO creature_action (id, description, action_id, action_arg_id, range_min, range_max, cooldown, windup, min_damage, max_damage, damage_type) " +
                    $"SELECT {a.Id}, {S(a.Description)}, {a.ActionId}, {a.ArgId}, {N(a.RangeMin)}, {N(a.RangeMax)}, {a.Cooldown}, {a.Windup}, {a.MinDamage}, {a.MaxDamage}, {a.DamageType} " +
                    $"FROM entityclass e WHERE e.id = {ThraxClass} AND NOT EXISTS (SELECT 1 FROM creature_action x WHERE x.id = {a.Id});";

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

            yield return "INSERT INTO creature_loot_binding (map_context_id, creature_id, profile_key) " +
                $"SELECT {Wilderness}, {Soldier}, {S(GuardsLootProfile)} FROM loot_profile p WHERE p.profile_key = {S(GuardsLootProfile)} " +
                $"AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {Soldier}) " +
                $"AND NOT EXISTS (SELECT 1 FROM creature_loot_binding x WHERE x.map_context_id = {Wilderness} AND x.creature_id = {Soldier});";

            foreach (var sql in MissionRows())
                yield return sql;
        }

        public static IEnumerable<string> Down()
        {
            foreach (var sql in InstanceMissionSql.Remove(Revision, Missions))
                yield return sql;

            yield return $"DELETE FROM creature_loot_binding WHERE map_context_id = {Wilderness} AND creature_id = {Soldier} AND profile_key = {S(GuardsLootProfile)};";

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

        private const string Wiki = "user-supplied: Tabula Rasa wiki, Guardian Prominence";
        private const string Guide = "user-supplied: Guardian Prominence instance walkthrough (missions, places, bosses and rewards)";

        // Experience and credits ours, at the Valverde Pools' level 35 missions' rate (Bane Jamming: 120,000 and 7,000).
        public const uint CallistoExperience = 90000, CallistoCredits = 6600;
        public const uint ChamberExperience = 120000, ChamberCredits = 7200;
        public const uint RadioExperience = 110000, RadioCredits = 7000;
        public const uint ChainExperience = 140000, ChainCredits = 7600;

        // The walkthrough's rewards, by its names; the manufacturers' names are not in the client's tables, so each is the
        // lowest template of the line, Uncommon where the line has one (the repair tools have none: Rare), whose band
        // holds the mission's level.
        public const uint BioHelmet = 19008;             // Dynamo Bio Armor Helmet: Armor_T3_Bio_V02_UNC_Helmet_35_to_39
        public const uint HazmatVest = 20931;            // Dynamo Hazmat Armor Vest: Armor_T2_Hazmat_V02_UNC_Vest_34_to_38
        public const uint MotorAssistVest = 29227;       // Dynamo Motor Assist Armor Vest: Armor_T1_MotorAssist_V04_UNC_Vest_33_to_37
        public const uint ReflectiveVest = 36168;        // Teleract Reflective Armor Vest: Armor_T2_Reflective_V02_UNC_Vest_34_to_38
        public const uint DirectRepairTool = 18259;      // ChiTech Direct Repair Tool: Tool_Avatar_Field_Repair_Direct_RAR_35_to_39
        public const uint CryogenicDispenser = 45747;    // Teleract Cryogenic Dispenser: Weapon_Avatar_PropellantGun_Ice_UNC_37_to_41
        public const uint IncendiaryShotgun = 14861;     // Shinobi Incendiary Shotgun: Weapon_Avatar_Shotgun_v1_Fire_UNC_37_to_41
        public const uint ElectricTorqueshell = 49576;   // AccuMax Electronic Torqueshell Rifle: Weapon_Avatar_Torqueshell_Rifle_Electric_UNC_38_to_42
        public const uint IncendiaryLauncher = 47912;    // Teleract Incendiary RPG Launcher: Weapon_Avatar_GrenadeLauncher_Fire_UNC_35_to_39
        public const uint LeechGun = 47808;              // ChiTech Leech Gun: Weapon_Avatar_LeechGun_Physical_UNC_34_to_38
        public const uint IncendiaryNetGun = 45812;      // Animatics Incendiary Net Gun: Weapon_Avatar_NetGun_Fire_UNC_36_to_40
        public const uint PulseShotgun = 15025;          // Animatics Pulse Shotgun: Weapon_Avatar_Shotgun_v1_EMP_UNC_37_to_41
        public const uint RadialRepairTool = 115798;     // Phoenix Radial Repair Tool: Tool_Avatar_Field_Repair_Area_RAR_40_to_44
        public const uint FlameThrower = 48752;          // Teleract Flame Thrower: Weapon_Avatar_PropellantGun_Fire_UNC_37_to_41
        public const uint EclipseRifle = 14614;          // Eclipse Rifle: Weapon_Avatar_Rifle_Physical_UNC_36_to_40
        public const uint Torqueshell = 45796;           // AccuMax Torqueshell Rifle: Weapon_Avatar_Torqueshell_Rifle_Physical_UNC_38_to_42

        /// <summary>The scenario events the Game raises: No Soap Radio's tower n destroyed is event n.</summary>
        public static uint TowerEvent(int index) => Towers[index].Event;

        private static IEnumerable<string> MissionRows()
        {
            var sql = new List<string>();
            FateOfTheCallistoRows(sql);
            ChamberOfEternityRows(sql);
            NoSoapRadioRows(sql);
            ChainOfCommandRows(sql);
            return sql;
        }

        /// <summary>Sonia Anand's: the flight recorder, then the crew; Professor Anand takes it.</summary>
        private static void FateOfTheCallistoRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, FateOfTheCallisto, "The Fate Of The Callisto", 3268, Sonia, Professor, 36, Category);
            mission.Reward(CallistoExperience, CallistoCredits, selectableItems: Choice(BioHelmet, HazmatVest, MotorAssistVest, ReflectiveVest));
            mission.LevelAtLeast(MinimumLevel);

            mission.Objective(FindRecorder, 3272, 7744, 1, MissionObjectiveState.Incomplete);
            mission.Objective(FindCrew, 3275, 7745, 2);

            mission.Transition(FindRecorder);
            mission.Conversation(FindRecorder, RecorderPackage);
            mission.RevealAndActivate(FindRecorder, FindCrew);

            mission.Transition(FindCrew);
            mission.Conversation(FindCrew, ProfessorPackage);
            mission.Action(FindCrew, 3, MissionActionKind.GrantReward, reward: 1);

            var scene = Scene(new MissionSceneDefinition
            {
                Dialogue = new() { new(FindRecorder, RecorderPackage, 1, transitionId: 1) }
            });
            scene.Actors["flight-recorder"] = new SceneActorDefinition("flight-recorder", SceneActorKind.Object, RecorderClass,
                new ScenePosition(RecorderAt.X, RecorderAt.Y, RecorderAt.Z), 0.6,
                Conversation: new SceneObjectConversation(FateOfTheCallisto, FindRecorder, RecorderPackage, 1));
            scene.Sequences[0] = new SceneSequenceDefinition
            {
                World = new List<WorldIntent> { new EnsureActorIntent("ensure-flight-recorder", "flight-recorder") }
            };
            Marker(mission, scene, FindRecorder, RecorderAt, 10);
            Marker(mission, scene, FindCrew, ProfessorAt, 10);
            Evidence(mission, "Researcher Sonia Anand at the way in gives it (offer 3270: 'Ranger Tomnel and I ... bail out in time'); " +
                "Professor Anand (package303, 'Who are you? Identify yourself!') takes it. The Callisto's flight recorder (class10630, package612, " +
                "its message 3274) by the wreck at the guide's (-150, 196); the professor with the survivors in the cave under the temple's north " +
                "wall (-55, 303, 79), the guide's way down. Missions from level 36 (the walkthrough).");
            mission.Enable(scene);
        }

        /// <summary>Professor Anand's: the Guardian Stone opens the chamber, and the Logos in it; he takes it.</summary>
        private static void ChamberOfEternityRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, ChamberOfEternity, "The Chamber Of Eternity", 7873, Professor, Professor, 38, Category);
            mission.Reward(ChamberExperience, ChamberCredits, selectableItems: Choice(DirectRepairTool, CryogenicDispenser, IncendiaryShotgun, ElectricTorqueshell));
            mission.LevelAtLeast(MinimumLevel);
            mission.Prerequisite(FateOfTheCallisto, 2);

            mission.Objective(RetrieveLogos, 7901, 7902, 1, MissionObjectiveState.Incomplete);
            mission.Transition(RetrieveLogos);
            mission.Progress(RetrieveLogos, MissionProgressEventKind.LogosAcquired, FutureLogos);
            mission.Action(RetrieveLogos, 1, MissionActionKind.GrantReward, reward: 1);

            var scene = Scene(new MissionSceneDefinition
            {
                ExistingFactObjectiveIds = new() { RetrieveLogos },
                Items = new()
                {
                    new MissionItemBinding(GuardianStoneKey, GuardianStoneTemplate, MissionItemScope.AssignmentIssued, 1,
                        MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove)
                },
                AcceptanceItems = new()
                {
                    new IssueMissionItemIntent("issue-guardian-stone", ChamberOfEternity, GuardianStoneKey, GuardianStoneTemplate, 1)
                }
            });
            scene.Sequences[0] = new SceneSequenceDefinition();
            Marker(mission, scene, RetrieveLogos, Chamber, 12);
            Evidence(mission, "Professor Anand gives and takes it, once The Fate Of The Callisto is done. He gives The Guardian Stone " +
                "(MisGuardpromItemElohArtifact49667, Luminary Sampei's) to open the chamber's 'strange lock': the Chamber Of Eternity's door " +
                "(UsableDoorElohV01, usable name2) in the map's ArchElohDoorJamb (328, 332, 256), opened for the copy by a member on it or done " +
                "with it (GuardianProminence). The Logos within is Future (logos58), as the Vogren Hologram's 1047; one already owned counts.");
            mission.Enable(scene);
        }

        public const string GuardianStoneKey = "guardian-stone";

        /// <summary>Cronauer's: the five jamming towers; he takes it, and calls in the reinforcements.</summary>
        private static void NoSoapRadioRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, NoSoapRadio, "No Soap Radio", 3349, Cronauer, Cronauer, 37, Category);
            mission.Reward(RadioExperience, RadioCredits, selectableItems: Choice(IncendiaryLauncher, LeechGun, IncendiaryNetGun, PulseShotgun));
            mission.LevelAtLeast(MinimumLevel);
            var scene = Scene();

            foreach (var t in Towers)
            {
                mission.Objective(t.Objective, t.Name, t.Body, t.Objective - 1, MissionObjectiveState.Incomplete);
                mission.Transition(t.Objective);
                mission.Progress(t.Objective, MissionProgressEventKind.ScenarioEvent, t.Event, counter: Scenario);
                Marker(mission, scene, t.Objective, t.At, 8);
            }
            mission.Action(Towers[0].Objective, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The Biomechanical Jamming Towers", Towers.Select(t => t.Event).ToArray());
            Evidence(mission, "Communications Officer Cronauer, at his radio with the AFS at the Temple of the Ages, gives and takes it. Five " +
                "Biomechanical Jamming Towers (usable name4, on UsableInertDestBaneConsoleBrainBase: the client's MisGuardPromThraxJammingUnit10645 " +
                "is an NPC, which the server neither spawns nor lets be shot): two in the open field by the temple, one on the way back towards " +
                "the bridge, two on Mount Epoch (the walkthrough's). Each destroyed is its objective for everyone on it, and stays down in the copy. " +
                "Turned in, the reinforcements come to the camp (GuardianProminence).");
            mission.Enable(scene);
        }

        /// <summary>Danchekker's: the three commanders; he takes it. Taking it brings the Juggernauts on the camp.</summary>
        private static void ChainOfCommandRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, ChainOfCommand, "Breaking The Chain Of Command", 3288, Danchekker, Danchekker, 40, Category);
            mission.Reward(ChainExperience, ChainCredits, selectableItems: Choice(RadialRepairTool, FlameThrower, EclipseRifle, Torqueshell));
            mission.LevelAtLeast(MinimumLevel);
            var scene = Scene(events: false);

            foreach (var (objective, name, body, boss, at, ordinal) in new[]
                     {
                         (KillHorga, 3292U, 7753U, Horga, HorgaAt, 1U), (KillZor, 3295U, 7754U, Zor, ZorAt, 2U), (KillLurge, 3296U, 7755U, Lurge, LurgeAt, 3U)
                     })
            {
                mission.Objective(objective, name, body, ordinal, MissionObjectiveState.Incomplete);
                mission.Transition(objective);
                mission.Progress(objective, MissionProgressEventKind.CreatureKilled, boss);
                Marker(mission, scene, objective, at, 12);
            }
            mission.Action(KillHorga, 1, MissionActionKind.GrantReward, reward: 1);

            Evidence(mission, "Lieutenant Danchekker at the Temple of the Ages gives and takes it ('Now stand back, I'm about to unleash the dogs " +
                "of war!'): taking it, two Juggernauts come out of the open field at the camp, which its soldiers beat if left alone " +
                "(GuardianProminence). Thrax Taskmaster Horga (40, rifleman) across the bridge on Mount Epoch, Thrax Overseer Lurge (41, " +
                "grenadier) at the mouth of the cave to the Chamber of Eternity, Thrax Preceptor Zor (40, engineer) at the chamber's door: the " +
                "walkthrough's. The client's objective4 (no texts) and objective7 (return to Danchekker, package617) are left out: the turn-in is his.");
            mission.Enable(scene);
        }

        private static InstanceMissionSql.RewardItem[] Choice(params uint[] templates) =>
            templates.Select(template => new InstanceMissionSql.RewardItem(template, 1)).ToArray();

        private static void Marker(InstanceMissionSql mission, MissionSceneDefinition scene, uint objective, Vector3 at, double radius)
        {
            var id = MissionIndicator.UnnamedFrom + objective;
            mission.Indicator(objective, id, at.X, at.Y, at.Z, radius);
            scene.IndicatorMaps ??= new Dictionary<uint, Dictionary<uint, uint>>();
            if (!scene.IndicatorMaps.TryGetValue(objective, out var maps))
                scene.IndicatorMaps[objective] = maps = new Dictionary<uint, uint>();
            maps[id] = Prominence;
        }

        private static MissionSceneDefinition Scene(bool events = true) => Scene(new MissionSceneDefinition(), events);

        private static MissionSceneDefinition Scene(MissionSceneDefinition scene, bool events = true)
        {
            scene.Requirement ??= new CustomRequirement("character.starting-experience-completed");
            scene.Script ??= events ? "data.sequence" : null;
            return scene;
        }

        private static void Evidence(InstanceMissionSql mission, string reconstruction)
        {
            mission.Evidence(1, MissionEvidenceSourceKind.Client, "trpython:data/generated/client/missionobjective.pyo_dis",
                "Client1.16.5 mission, objective, text and conversation identities.");
            mission.Evidence(2, MissionEvidenceSourceKind.Documentation, Guide, "The walkthrough: people, places, order, level and rewards.");
            mission.Evidence(3, MissionEvidenceSourceKind.Documentation, Wiki, "The wiki: the missions' givers, the Bane's levels (38-40).");
            mission.Evidence(4, MissionEvidenceSourceKind.Reconstruction, "GuardianProminenceContent", reconstruction);
        }

        #endregion
    }
}
