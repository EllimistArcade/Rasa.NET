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
    /// Tahrendra Base (adv_arieki_torden_mires_tahrendrabase, 2125), off the Mires, as the Tabula
    /// Rasa wiki's page and Ten Ton Hammer's walkthrough tell it: the AFS pushing into the Bane's
    /// staging base, and its three missions, in the client's own names and texts.
    ///
    /// Who and what:
    ///  - Captain Tillery, beside the field medic at the way in, gives Break on Through: two
    ///    Salvaged Detonators from the crates either side of the Guard Lounge, blown at the doors
    ///    of the two Guard Barracks, which seals them. With both done he is in the Security Check
    ///    Point room, takes it there and gives Bringing the House Down.
    ///  - Bringing the House Down: Taskmaster Eitak in Wormhole Security (Security Station 1),
    ///    with Lightbenders on its balcony; the Bane CommLink Terminal below its switch (package
    ///    1634); Overseer Xixl in Lower Block Security (Security Station 2), where a Bane
    ///    transporter beams more in; back to Tillery, who hands over the Bot Virus (package 1631);
    ///    and Upper Block Security (Security Station 3), its floor and its three balconies all
    ///    dead at once. Radioed in. The optional side: five Bane DNA samples (Biological Material
    ///    Samples, from the base's Bane), the AFS teleporter across from Security Station 2's door
    ///    switched on, Specialist Donnaway beamed in by it (package 1633), who takes the samples
    ///    for a Biohazard Charge, set on the red light by that door; and the Warden Bots, whose
    ///    programming the Bot Virus turns when they are shot.
    ///  - What Goes Around, offered by radio after it: Donnaway at the wormhole room's door, taken
    ///    in to its mark, where Dominator Ryxnl comes at her; three minutes and three waves while
    ///    she uploads the virus; then the Eloh's hologram (package 1635). Tillery takes it.
    ///  - The Bane at levels 28 to 31: the base's own Thrax soldiers ("Tahrendra Base Guard"),
    ///    grenadiers, technicians, caretakers, hunters and its mortar, Lightbender Sharpshooters on
    ///    the balconies; Taskmaster Eitak at 29, Overseer Xixl at 30, Dominator Ryxnl at 31. The
    ///    seed's Mires Bane on the map (levels 36 and 37) are the base's own now.
    /// The barracks' doors, the crates, the terminal, the teleporters, the Bio Charge, the Warden
    /// Bots, Donnaway, the waves and the hologram are the server's, per copy of the base
    /// (TahrendraBase).
    ///
    /// Health and damage: the Energy Weapon Center's level 28-29 Bane rows' (the Pools' and the
    /// Plateau's attacks); the Lightbenders' and the Warden Bots' ours, scaled from Kardash's and
    /// the Incline's.
    /// </summary>
    public static class TahrendraBaseContent
    {
        public const string Revision = "tahrendra_1_16_5";
        public const uint Tahrendra = 2125, Mires = 1759;

        /// <summary>missioncategorylanguage: "Instance (Tahrendra Base)".</summary>
        public const uint Category = 8;

        public const uint BreakOnThrough = 1192, BringingTheHouseDown = 1195, WhatGoesAround = 1196;

        public static readonly uint[] Missions = { BreakOnThrough, BringingTheHouseDown, WhatGoesAround };

        // The NPC packages of the missions' conversations (objectiveconversation).
        public const uint TilleryPackage = 1631, DonnawayPackage = 1633, ConsolePackage = 1634, HologramPackage = 1635;

        // The field medic at the way in, already in the world.
        public const uint Medic = 500104;

        // New people and rows.
        public const uint Tillery = 786001, Donnaway = 786002, Hologram = 786003, WardenBot = 786004;
        public const uint Soldier = 786010, Grenadier = 786011, Technician = 786012, Caretaker = 786013, Hunter = 786014,
            Lightbender = 786015, Mortar = 786017;
        public const uint Eitak = 786020, Xixl = 786021, Ryxnl = 786022;

        /// <summary>The base's people: Tillery, Donnaway and the hologram are the copy's (TahrendraBase).</summary>
        public static readonly uint[] Afs = { Tillery, Donnaway, Hologram };

        /// <summary>The base's own Bane, whose bodies have DNA samples on them.</summary>
        public static readonly uint[] Bane = { Soldier, Grenadier, Technician, Caretaker, Hunter, Lightbender };

        // Items (class, template).
        public const uint DetonatorClass = 25666, DetonatorTemplate = 97538;   // MisTordenMiresTahrendraDoorBombs, "Salvaged Detonator"
        public const uint ChargeClass = 25667, ChargeTemplate = 97539;         // MisTordenMiresTahrendraBioCharge, "Biohazard Charge"
        public const uint SamplesClass = 25668, SamplesTemplate = 97540;       // MisTordenMiresTahrendraBioMaterials, "Biological Material Samples"
        public const uint VirusClass = 25669, VirusTemplate = 97541;           // MisTordenMiresTahrendraRobotCard, "Bot Virus"

        public const uint DetonatorsWanted = 2, SamplesWanted = 5, RobotsWanted = 3;

        // The objects the Game puts down.
        public const uint CrateClass = 7894;            // UsableTreasureDispBaneCrateV01
        public const uint BombClass = BombSitesWithAModel.BombClass;
        public const uint KeyboardClass = 10519;        // UsableNPCBaneKeyboard: the Bane CommLink Terminal
        public const uint TeleporterClass = 7895;       // UsableTwoStateHumTeleporter
        public const uint TransporterClass = 10603;     // UsableInertDestBaneTeleporterV01DELETEDUPE
        public const uint TransporterHitPoints = 3000;

        // Objectives (missionobjective), and the scenario every mission's events are in.
        public const uint Scenario = 1;
        public const uint FindExplosives = 1, SealBarracks = 2;
        public const uint SecureStation1 = 1, Samples = 2, SecurityConsole = 3, Teleporter = 4, SecureStation2 = 5, Robots = 6,
            SecureStation3 = 7, SpeakToDonnaway = 8, BioBomb = 9, ReturnToTillery = 10;
        public const uint Rendezvous = 1, EscortDonnaway = 2, Protect = 3, Recording = 4, Wave2 = 5, Wave3 = 6;

        // Events.
        public const uint BarracksSealed = 1;
        public const uint TeleporterOn = 1, BioBombSet = 2, RobotsGathered = 3, UpperBlockSecured = 4;
        public const uint DonnawayDelivered = 1, UploadDone = 2, Wave2Down = 3, Wave3Down = 4;

        #region Places

        /// <summary>The way in, from the Mires (map_link 65), and where a player arrives.</summary>
        public static readonly Vector3 Arrival = new(196f, 120.6f, 10.5f);

        public static readonly Vector3 TilleryAtMedic = new(189.2f, 116.6f, 39.2f);
        public static readonly Vector3 CheckPoint = new(16f, 112.2f, 11.4f);
        public static readonly Vector3 TilleryAtCheckPoint = new(28f, 112.4f, 2f);
        public static readonly Vector3 GuardLounge = new(28f, 120.6f, -75f);

        /// <summary>The Guard Lounge's crates, one either side of the room.</summary>
        public static readonly (Vector3 At, float Yaw)[] Crates =
        {
            (new Vector3(2f, 120.6f, -70f), 1.5707964f),
            (new Vector3(53.6f, 120.6f, -63f), -1.5707964f)
        };

        /// <summary>
        /// A Guard Barracks: the jamb its door is in (ArchBrannCorridorJamb014mV01, the dormitory's
        /// way in), where its bomb is set, its pool and the room.
        /// </summary>
        public sealed record Barracks(string Name, Vector3 Jamb, float Yaw, Vector3 BombAt, uint Pool, Vector3 Inside)
        {
            /// <summary>Where its door stands: the jamb's middle, 2 m along its own -Z.</summary>
            public Vector3 Door => Jamb + Vector3.Transform(new Vector3(0, 0, -2f), Quaternion.CreateFromYawPitchRoll(Yaw, 0f, 0f));
        }

        public const uint NorthBarracksPool = 786201, SouthBarracksPool = 786202;

        public static readonly Barracks[] GuardBarracks =
        {
            new("the north Guard Barracks", new Vector3(20f, 120f, 52f), 1.5707964f, new Vector3(15f, 120.6f, 52f), NorthBarracksPool,
                new Vector3(35.5f, 120.6f, 51.9f)),
            new("the south Guard Barracks", new Vector3(36f, 120f, -28f), 1.5707964f, new Vector3(31f, 120.6f, -28f), SouthBarracksPool,
                new Vector3(52.5f, 120.6f, -28.1f))
        };

        public static readonly Vector3 Station1 = new(-76f, 120.2f, 48.6f);
        public static readonly Vector3 Terminal = new(-89f, 120.2f, 61f);
        public static readonly Vector3 Station2 = new(-2f, 80.3f, 72f);
        public static readonly Vector3 XixlAt = new(-5f, 80.3f, 85f);
        public static readonly Vector3 Transporter = new(8f, 80.2f, 78f);

        /// <summary>The red light by Security Station 2's door (ArchBrannCorridorJamb014mV01 at (-28, 80, 72)), where the charge is set.</summary>
        public static readonly Vector3 BioSite = new(-32f, 80.74f, 72f);

        /// <summary>The AFS teleporter across from Security Station 2's door, and where Donnaway comes in.</summary>
        public static readonly Vector3 AfsTeleporter = new(-46f, 80.6f, 70f);
        public static readonly Vector3 DonnawayBeamsIn = new(-42f, 80.97f, 72f);

        /// <summary>The lower block's corridors, where the Bane have the DNA samples.</summary>
        public static readonly Vector3 LowerBlock = new(-60f, 80.6f, 72f);

        public static readonly Vector3 Station3 = new(-134f, 144.4f, -64f);
        public static readonly Vector3 UpperBlockWay = new(-70f, 136f, -55f);

        /// <summary>The Warden Bots on the way up to Upper Block Security.</summary>
        public static readonly Vector3[] WardenBots =
        {
            new(-60f, 128.7f, -40f), new(-76f, 128.6f, -70f), new(-60f, 144.7f, -55f), new(-90.3f, 144.6f, -64.5f)
        };

        public static readonly Vector3 RendezvousAt = new(-100f, 112.7f, 12f);
        public static readonly Vector3 WormholeMark = new(-150f, 112.2f, 12f);
        public static readonly Vector3 Wormhole = new(-166f, 111.6f, 12f);
        public static readonly Vector3 DonnawayAtWork = new(-156.4f, 112.2f, 19.6f);
        public static readonly Vector3 HologramAt = new(-150f, 112.4f, 0f);

        /// <summary>Where the waves come from: the main door behind, and the wormhole room's two balconies.</summary>
        public static readonly Vector3 MainDoor = new(-105f, 112.6f, 12f);
        public static readonly Vector3[] Balconies = { new(-166f, 120.6f, -16f), new(-166f, 120.6f, 38.2f) };

        #endregion

        #region Rows

        public enum TargetCategoryRow : uint { Hostile = 0, Friendly = 1, Neutral = 3 }

        public sealed record Beast(uint Id, string Comment, uint ClassId, TargetCategoryRow Faction, uint Level, uint Hp,
            uint Armor, uint NameId, uint RunSpeed, uint WalkSpeed, params uint[] Actions);

        public sealed record Act(uint Id, string Description, uint ActionId, uint ArgId, double RangeMin, double RangeMax,
            uint Cooldown, uint Windup, uint MinDamage, uint MaxDamage, uint DamageType);

        public sealed record Spot(uint Id, double X, double Y, double Z, double Rotation, uint RespawnTenths, double Radius,
            params (uint Creature, uint Min, uint Max)[] Slots);

        private const uint ThraxPistolClass = 20757, GrenadierClass = 9354, TechnicianClass = 7043, CaretakerClass = 9244,
            HunterClass = 10166, LightbenderClass = 7120, MortarClass = 7482, CommanderClass = 10504, WardenBotClass = 6961,
            HologramClass = 24591;

        // The base world's attacks, the Energy Weapon Center's at these levels: the Pools' Thrax and Hunters, the Plateau's
        // caretakers, technicians, commanders and mortar.
        private const uint SoldierKick = 53137, SoldierPistol = 53138, GrenadierLob = 53139, GrenadierRifle = 53140,
            CaretakerAttack = 53131, CaretakerHeal = 62021, CaretakerRevive = 62022, HunterNet = 53146, HunterGun = 53147,
            HunterPet = 65036, TechnicianGun = 70028, TechnicianHeal = 62047, TechnicianTurret = 65016,
            CommanderKick = 53132, CommanderRifle = 53133, MortarShell = 75204;

        public const uint LightbenderBeam = 786101, WardenLaser = 786102;

        public static readonly Act[] Actions =
        {
            new(LightbenderBeam, "Lightbender Sharpshooter - Tahrendra Base beam 1/149", 1, 149, 0.5, 40, 2500, 0, 105, 158, 6),
            new(WardenLaser, "Refitted Warden Bot - Tahrendra Base warden_bot_laser", 273, 1, 1, 60, 6000, 1999, 150, 225, 6)
        };

        public static readonly Beast[] Creatures =
        {
            new(Tillery, "Captain Tillery - Tahrendra Base", SwapsetMale, TargetCategoryRow.Friendly, 30, 3000, 0, 9490, 0, 0),
            new(Donnaway, "Specialist Donnaway - Tahrendra Base", SwapsetFemale, TargetCategoryRow.Friendly, 29, 3000, 0, 9497, 9, 5),
            new(Hologram, "Holographic Eloh - Tahrendra Base wormhole room", HologramClass, TargetCategoryRow.Friendly, 30, 3000, 0, 9078, 0, 0),
            new(WardenBot, "Refitted Warden Bot - Tahrendra Base", WardenBotClass, TargetCategoryRow.Neutral, 29, 4000, 150, 9529, 9, 5, WardenLaser),
            new(Soldier, "Tahrendra Base Guard - Thrax soldier", ThraxPistolClass, TargetCategoryRow.Hostile, 28, 2230, 120, 8738, 9, 5,
                SoldierKick, SoldierPistol),
            new(Grenadier, "Thrax Grenadier - Tahrendra Base", GrenadierClass, TargetCategoryRow.Hostile, 29, 2432, 120, 0, 9, 5,
                GrenadierLob, GrenadierRifle),
            new(Technician, "Thrax Technician - Tahrendra Base", TechnicianClass, TargetCategoryRow.Hostile, 28, 2973, 130, 8058, 9, 5,
                TechnicianGun, TechnicianHeal, TechnicianTurret),
            new(Caretaker, "Caretaker - Tahrendra Base", CaretakerClass, TargetCategoryRow.Hostile, 29, 3242, 120, 362, 9, 5,
                CaretakerAttack, CaretakerHeal, CaretakerRevive),
            new(Hunter, "Hunter - Tahrendra Base", HunterClass, TargetCategoryRow.Hostile, 29, 2432, 120, 389, 9, 5,
                HunterNet, HunterGun, HunterPet),
            new(Lightbender, "Lightbender Sharpshooter - Tahrendra Base", LightbenderClass, TargetCategoryRow.Hostile, 29, 2230, 110, 10366, 9, 5,
                LightbenderBeam),
            new(Mortar, "Bane Mortar - Tahrendra Base", MortarClass, TargetCategoryRow.Hostile, 29, 6187, 150, 0, 0, 0, MortarShell),
            new(Eitak, "Taskmaster Eitak - Tahrendra Base Security Station 1", CommanderClass, TargetCategoryRow.Hostile, 29, 7000, 160, 9486, 9, 5,
                CommanderKick, CommanderRifle),
            new(Xixl, "Overseer Xixl - Tahrendra Base Security Station 2", CommanderClass, TargetCategoryRow.Hostile, 30, 8000, 170, 9487, 9, 5,
                CommanderKick, CommanderRifle),
            new(Ryxnl, "Dominator Ryxnl - Tahrendra Base wormhole room", GrenadierClass, TargetCategoryRow.Hostile, 31, 9500, 180, 9489, 9, 5,
                GrenadierLob, GrenadierRifle)
        };

        // Looks: the AFS officers' blue of the seed's officers, and the mortar's launcher.
        public const uint FemaleFace = 24009, FemaleHair = 3672;

        private static Slot[] Uniform(uint hair, uint face, uint skin, uint weapon) => new[]
        {
            new Slot(2, OfficerBoots, OfficerBlue), new Slot(13, weapon, 1), new Slot(14, hair, DarkHair),
            new Slot(15, OfficerTunic, OfficerBlue), new Slot(16, OfficerLegs, OfficerBlue), new Slot(17, face, skin)
        };

        public static readonly (uint Creature, Slot[] Look)[] Looks =
        {
            (Tillery, Uniform(9782, 24032, SkinMedium, RifleAvatar)),
            (Donnaway, Uniform(FemaleHair, FemaleFace, SkinLight, PistolRedshirt)),
            // The mortar's launcher in its weapon slot, as every mortar's (WildernessMortarLaunchers).
            (Mortar, new[] { new Slot(WildernessMortarLaunchers.WeaponSlot, WildernessMortarLaunchers.Launcher, WildernessMortarLaunchers.Color) })
        };

        /// <summary>Who speaks which package.</summary>
        public static readonly (uint Creature, uint Package, string Comment)[] Packages =
        {
            (Tillery, TilleryPackage, "Captain Tillery - Tahrendra Base"),
            (Donnaway, DonnawayPackage, "Specialist Donnaway - Tahrendra Base"),
            (Hologram, HologramPackage, "Holographic Eloh - Tahrendra Base")
        };

        public const uint BarracksRespawn = 450, HostileRespawn = 3000, NamedRespawn = 6000;

        public const uint EitakPool = 786204, XixlPool = 786208;

        /// <summary>Upper Block Security's pools: all of them dead at once is it secured (TahrendraBase).</summary>
        public static readonly uint[] UpperBlockPools = { 786209, 786210, 786211, 786212 };

        /// <summary>The base's pools, all on map 2125.</summary>
        public static readonly Spot[] Pools =
        {
            // The Guard Barracks: the reinforcements, until their doors are sealed.
            new(NorthBarracksPool, 25, 120.6, 52, -1.6, BarracksRespawn, 3, (Soldier, 2, 2)),
            new(SouthBarracksPool, 45, 120.6, -28, -1.6, BarracksRespawn, 3, (Soldier, 1, 1), (Grenadier, 1, 1)),
            // Wormhole Security (Security Station 1): Taskmaster Eitak, his guards, and Lightbenders on the balcony.
            new(786203, -82, 120.3, 40, 0, HostileRespawn, 2, (Soldier, 2, 2)),
            new(EitakPool, Station1.X, Station1.Y, Station1.Z, 3.1, NamedRespawn, 0, (Eitak, 1, 1)),
            new(786205, -96.2, 128.6, 52.3, 1.6, HostileRespawn, 0, (Lightbender, 1, 1)),
            new(786206, -55.5, 128.6, 50.7, -1.6, HostileRespawn, 0, (Lightbender, 1, 1)),
            // Lower Block Security (Security Station 2): Overseer Xixl and his guard.
            new(786207, 2, 80.2, 60, 3.1, HostileRespawn, 2, (Soldier, 2, 2), (Caretaker, 1, 1)),
            new(XixlPool, XixlAt.X, XixlAt.Y, XixlAt.Z, -1.6, NamedRespawn, 0, (Xixl, 1, 1)),
            // Upper Block Security (Security Station 3): its floor, and Hunters and Lightbenders on its balconies.
            new(786209, Station3.X, Station3.Y, Station3.Z, 1.6, HostileRespawn, 2, (Soldier, 2, 2), (Caretaker, 1, 1)),
            new(786210, -136.4, 152.5, -45.6, 3.1, HostileRespawn, 0, (Hunter, 1, 1)),
            new(786211, -140.4, 152.6, -85.3, 0, HostileRespawn, 0, (Hunter, 1, 1)),
            new(786212, -127, 152.7, -57.5, 1.6, HostileRespawn, 1, (Lightbender, 2, 2))
        };

        /// <summary>The seed's Mires Bane on the map, and the base's own that stand in for them.</summary>
        public static readonly (uint Seed, uint Ours)[] Recast =
        {
            (531055, Soldier), (531056, Grenadier), (531057, Technician), (531058, Caretaker), (531061, Hunter), (592009, Mortar)
        };

        /// <summary>The seed's pools on the map whose Bane the base's own are now.</summary>
        public static readonly uint[] SeedPools =
        {
            531439, 531440, 531441, 531442, 531443, 531444, 531445, 531446, 531447, 531448, 531449, 531450, 531451, 531452,
            531453, 531454, 531455, 531456, 531457, 531458, 531459, 592333
        };

        #endregion

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
                yield return SpawnPool(p.Id, Tahrendra, p.X, p.Y, p.Z, p.Rotation, p.RespawnTenths, p.Radius, p.Slots);

            // The seed's Mires Bane, levels 36 and 37, are the base's own.
            foreach (var sql in RecastPools(Recast))
                yield return sql;

            foreach (var sql in MissionRows())
                yield return sql;
        }

        public static IEnumerable<string> Down()
        {
            foreach (var sql in InstanceMissionSql.Remove(Revision, Missions))
                yield return sql;

            foreach (var sql in RecastPools(Recast.Select(r => (r.Ours, r.Seed)).ToArray()))
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

        /// <summary>The seed's pools on the map, each slot of one row put to the other, where that row is there.</summary>
        private static IEnumerable<string> RecastPools((uint From, uint To)[] pairs)
        {
            var pools = string.Join(", ", SeedPools);

            foreach (var (from, to) in pairs)
                for (var slot = 1; slot <= 6; slot++)
                    yield return $"UPDATE spawnpool SET creature_{slot}_Id = {to} WHERE id IN ({pools}) AND map_context_id = {Tahrendra} " +
                        $"AND creature_{slot}_Id = {from} AND EXISTS (SELECT 1 FROM creature c WHERE c.id = {to});";
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

        private const string Guide = "user-supplied: Ten Ton Hammer, Tahrendra Base walkthrough";
        private const string Wiki = "user-supplied: Tabula Rasa wiki, Tahrendra Base";

        // Experience and credits ours, as the Energy Weapon Center's at these levels.
        public const uint BreakExperience = 40000, BreakCredits = 4100, HouseExperience = 55000, HouseCredits = 5100,
            AroundExperience = 70000, AroundCredits = 5600;

        // The rewards to choose from, the walkthrough's: the client's templates made for these two missions, a run of
        // them in the template table (116766-116772, and the extractor 117023).
        public const uint SalvageTool = 116766;          // Tool_Avatar_Salvage_30, "Dynamo Salvage Tool"
        public const uint CipherTool = 116767;           // Tool_Avatar_Cipher_30, "Dynamo Cipher Tool"
        public const uint TissueExtractor = 117023;      // Tool_Avatar_Tissue_Extractor_30, "Prodigy Tissue Extractor"
        public const uint HealingDisc = 116768;          // Tool_Avatar_Healing_Disc_Area_RAR_30_to_34, "Wellcare Conical Healing Disc"
        public const uint GravitonVest = 116769;         // Armor_T3_Graviton_V07_ELT_Vest_30_to_34, "Voltrox Graviton Armor Vest"
        public const uint HazmatLegs = 116770;           // Armor_T2_Hazmat_V06_ELT_Legs_29_to_33, "Phoenix Hazmat Armor Legs"
        public const uint ReflectiveBoots = 116771;      // Armor_T2_Reflective_V06_ELT_Boots_29_to_33, "Voltrox Reflective Armor Boots"
        public const uint MotorAssistHelmet = 116772;    // Armor_T1_MotorAssist_V12_ELT_Helmet_28_to_32, "Voltrox Motor Assist Armor Helmet"

        private static IEnumerable<string> MissionRows()
        {
            var sql = new List<string>();
            BreakOnThroughRows(sql);
            BringingTheHouseDownRows(sql);
            WhatGoesAroundRows(sql);
            return sql;
        }

        /// <summary>Tillery's: two Salvaged Detonators from the Guard Lounge, blown at the barracks' doors.</summary>
        private static void BreakOnThroughRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, BreakOnThrough, "Break on Through", 9294, Tillery, Tillery, 28, Category);
            mission.Reward(BreakExperience, BreakCredits);
            var scene = Scene();

            mission.Objective(FindExplosives, 11768, 11769, 1, MissionObjectiveState.Incomplete);
            mission.Objective(SealBarracks, 11770, 11771, 2);

            mission.Transition(FindExplosives);
            mission.Progress(FindExplosives, MissionProgressEventKind.ItemAcquired, DetonatorClass, target: DetonatorsWanted);
            mission.RevealAndActivate(FindExplosives, SealBarracks);

            mission.Transition(SealBarracks);
            mission.Progress(SealBarracks, MissionProgressEventKind.ScenarioEvent, BarracksSealed, counter: Scenario);
            mission.Action(SealBarracks, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The Guard Barracks sealed", BarracksSealed);
            Marker(mission, scene, FindExplosives, 1, GuardLounge, 25);
            for (var i = 0; i < GuardBarracks.Length; i++)
                Marker(mission, scene, SealBarracks, (uint)(i + 1), GuardBarracks[i].BombAt, 5);
            Evidence(mission, "Captain Tillery, beside the field medic at the way in, gives it; with the barracks sealed he is in the " +
                "Security Check Point room and takes it there. The explosives are in the crates either side of the Guard Lounge (the " +
                "walkthrough's 'mission markers in this room are not accurate'): a Salvaged Detonator97538 each, two wanted; each " +
                "blown at the door of a Guard Barracks (the client's two, at (35, 52) and (52, -28)) seals it. The walkthrough's " +
                "rewards, modification recipes for Regen Bonus, EMP Resistance and Debuff Physical Resist, are left out: the " +
                "client's Recipe_Modification templates do not say which recipe each is.");
            mission.Enable(scene);
        }

        /// <summary>Tillery's, radioed in: the three security stations, the terminal, and the optional DNA, Donnaway and Warden Bots.</summary>
        private static void BringingTheHouseDownRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, BringingTheHouseDown, "Bringing the House Down", 9311, Tillery, null, 29, Category);
            mission.Reward(HouseExperience, HouseCredits,
                selectableItems: new[] { Item(SalvageTool), Item(CipherTool), Item(TissueExtractor), Item(HealingDisc) });
            mission.Prerequisite(BreakOnThrough);
            mission.RadioTurnIn();
            var scene = Scene();

            mission.Objective(SecureStation1, 11794, 11795, 1, MissionObjectiveState.Incomplete);
            mission.Objective(SecurityConsole, 11796, 11797, 2);
            mission.Objective(Samples, 11801, 11802, 3, required: false);
            mission.Objective(SecureStation2, 11799, 11800, 4);
            mission.Objective(Teleporter, 11803, 11804, 5, required: false);
            mission.Objective(SpeakToDonnaway, 11805, 11806, 6, required: false);
            mission.Objective(BioBomb, 11808, 11809, 7, required: false);
            mission.Objective(ReturnToTillery, 11810, 11811, 8);
            mission.Objective(Robots, 11815, 11816, 9, required: false);
            mission.Objective(SecureStation3, 11813, 11814, 10);

            mission.Transition(SecureStation1);
            mission.Progress(SecureStation1, MissionProgressEventKind.CreatureKilled, Eitak);
            mission.RevealAndActivate(SecureStation1, SecurityConsole);

            // An object's conversation is the object's interaction (MissionApplication.TryApplyDialogue).
            mission.Transition(SecurityConsole);
            mission.Progress(SecurityConsole, MissionProgressEventKind.InteractionUsed, KeyboardClass);
            mission.RevealAndActivate(SecurityConsole, Samples);
            mission.RevealAndActivate(SecurityConsole, SecureStation2, 3);

            mission.Transition(Samples);
            mission.Progress(Samples, MissionProgressEventKind.ItemAcquired, SamplesClass, target: SamplesWanted);
            mission.RevealAndActivate(Samples, Teleporter);

            mission.Transition(Teleporter);
            mission.Progress(Teleporter, MissionProgressEventKind.ScenarioEvent, TeleporterOn, counter: Scenario);
            mission.RevealAndActivate(Teleporter, SpeakToDonnaway);

            mission.Transition(SpeakToDonnaway);
            mission.Conversation(SpeakToDonnaway, DonnawayPackage);
            mission.Action(SpeakToDonnaway, 1, MissionActionKind.ConsumeMissionItem,
                itemIntent: new ConsumeMissionItemIntent("donnaway-takes-samples", BringingTheHouseDown, "samples", SamplesWanted, MissionItemScope.CharacterOwned));
            mission.Action(SpeakToDonnaway, 2, MissionActionKind.IssueMissionItem,
                itemIntent: new IssueMissionItemIntent("donnaway-charge", BringingTheHouseDown, "charge", ChargeTemplate, 1));
            mission.RevealAndActivate(SpeakToDonnaway, BioBomb, 3);

            mission.Transition(BioBomb);
            mission.Progress(BioBomb, MissionProgressEventKind.ScenarioEvent, BioBombSet, counter: Scenario);
            mission.Action(BioBomb, 1, MissionActionKind.ConsumeMissionItem,
                itemIntent: new ConsumeMissionItemIntent("bio-charge-set", BringingTheHouseDown, "charge", 1, MissionItemScope.AssignmentIssued));

            mission.Transition(SecureStation2);
            mission.Progress(SecureStation2, MissionProgressEventKind.CreatureKilled, Xixl);
            mission.RevealAndActivate(SecureStation2, ReturnToTillery);

            mission.Transition(ReturnToTillery);
            mission.Conversation(ReturnToTillery, TilleryPackage);
            mission.Action(ReturnToTillery, 1, MissionActionKind.IssueMissionItem,
                itemIntent: new IssueMissionItemIntent("tillery-bot-virus", BringingTheHouseDown, "virus", VirusTemplate, 1));
            mission.RevealAndActivate(ReturnToTillery, Robots, 2);
            mission.RevealAndActivate(ReturnToTillery, SecureStation3, 4);

            mission.Transition(Robots);
            mission.Progress(Robots, MissionProgressEventKind.ScenarioEvent, RobotsGathered, counter: Scenario);

            mission.Transition(SecureStation3);
            mission.Progress(SecureStation3, MissionProgressEventKind.ScenarioEvent, UpperBlockSecured, counter: Scenario);
            mission.Action(SecureStation3, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "Bringing the House Down", TeleporterOn, BioBombSet, RobotsGathered, UpperBlockSecured);
            Marker(mission, scene, SecureStation1, 1, Station1, 15);
            Marker(mission, scene, SecurityConsole, 1, Terminal, 4);
            Marker(mission, scene, Samples, 1, LowerBlock, 40);
            Marker(mission, scene, Teleporter, 1, AfsTeleporter, 4);
            Marker(mission, scene, SpeakToDonnaway, 1, DonnawayBeamsIn, 6);
            Marker(mission, scene, BioBomb, 1, BioSite, 4);
            Marker(mission, scene, SecureStation2, 1, XixlAt, 15);
            Marker(mission, scene, ReturnToTillery, 1, TilleryAtCheckPoint, 6);
            Marker(mission, scene, Robots, 1, UpperBlockWay, 30);
            Marker(mission, scene, SecureStation3, 1, Station3, 15);
            scene.Items = new List<MissionItemBinding>
            {
                new("samples", SamplesTemplate, MissionItemScope.CharacterOwned, SamplesWanted,
                    MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain, MissionItemCleanupDisposition.Retain,
                    Drop: new MissionItemDropDefinition(Bane, Samples, Tahrendra, 50, 1)),
                Issued("charge", ChargeTemplate),
                Issued("virus", VirusTemplate)
            };
            Evidence(mission, "Captain Tillery gives it in the Security Check Point room once Break on Through is done; HQ radios its " +
                "end. Security Station 1 is Wormhole Security, the room left of the top of the stairs: Taskmaster Eitak9486 dead secures " +
                "it, Lightbenders on its balcony. The Bane Security Console is the CommLink Terminal below its switch, an object " +
                "conversation (package1634, 'access to their main data terminals'). Security Station 2 is Lower Block Security: Overseer " +
                "Xixl9487 dead, a Bane transporter beaming more in. Then back to Tillery (package1631), who has the weapons modified " +
                "to turn the Warden Bots (the Bot Virus97541); Security Station 3, Upper Block Security, is its floor and its balconies " +
                "all dead at once. The optional side, as the console's text has it: five Biological Material Samples97540 from the " +
                "base's Bane (one in two), the AFS teleporter across from Security Station 2's door switched on, Specialist Donnaway " +
                "(package1633) beamed in by it, who takes the samples for a Biohazard Charge97539, set on the door's red light; and " +
                "three Warden Bots shot with the virus.");
            mission.Enable(scene);
        }

        /// <summary>Offered by radio: Donnaway into the wormhole room, three minutes and three waves, the hologram; Tillery takes it.</summary>
        private static void WhatGoesAroundRows(List<string> sql)
        {
            var mission = new InstanceMissionSql(sql, Revision, WhatGoesAround, "What Goes Around ...", 9316, null, Tillery, 30, Category);
            mission.Reward(AroundExperience, AroundCredits,
                selectableItems: new[] { Item(GravitonVest), Item(HazmatLegs), Item(ReflectiveBoots), Item(MotorAssistHelmet) });
            mission.Prerequisite(BringingTheHouseDown);
            var scene = Scene();

            mission.Objective(Rendezvous, 11820, 11821, 1, MissionObjectiveState.Incomplete);
            mission.Objective(EscortDonnaway, 11823, 11824, 2);
            mission.Objective(Protect, 11825, 11826, 3);
            mission.Objective(Wave2, 11827, 11828, 4);
            mission.Objective(Wave3, 11829, 11830, 5);
            mission.Objective(Recording, 11831, 11832, 6);

            mission.Transition(Rendezvous);
            mission.Conversation(Rendezvous, DonnawayPackage);
            mission.RevealAndActivate(Rendezvous, EscortDonnaway);

            mission.Transition(EscortDonnaway);
            mission.Progress(EscortDonnaway, MissionProgressEventKind.ScenarioEvent, DonnawayDelivered, counter: Scenario);
            mission.RevealAndActivate(EscortDonnaway, Protect);
            mission.RevealAndActivate(EscortDonnaway, Wave2, 3);
            mission.RevealAndActivate(EscortDonnaway, Wave3, 5);

            mission.Transition(Wave2);
            mission.Progress(Wave2, MissionProgressEventKind.ScenarioEvent, Wave2Down, counter: Scenario);
            mission.Transition(Wave3);
            mission.Progress(Wave3, MissionProgressEventKind.ScenarioEvent, Wave3Down, counter: Scenario);

            mission.Transition(Protect);
            mission.Progress(Protect, MissionProgressEventKind.ScenarioEvent, UploadDone, counter: Scenario);
            mission.RevealAndActivate(Protect, Recording);

            mission.Transition(Recording);
            mission.Conversation(Recording, HologramPackage);
            mission.Action(Recording, 1, MissionActionKind.GrantReward, reward: 1);

            mission.Scenario(Scenario, "The wormhole", DonnawayDelivered, UploadDone, Wave2Down, Wave3Down);
            Marker(mission, scene, Rendezvous, 1, RendezvousAt, 6);
            Marker(mission, scene, EscortDonnaway, 1, WormholeMark, 6);
            Marker(mission, scene, Protect, 1, DonnawayAtWork, 15);
            Marker(mission, scene, Wave2, 1, DonnawayAtWork, 25);
            Marker(mission, scene, Wave3, 1, DonnawayAtWork, 25);
            Marker(mission, scene, Recording, 1, HologramAt, 5);

            mission.Channels(MissionChannel.Radio, MissionChannel.Npc, MissionOfferSourceDefinition.MissionRewarded(BringingTheHouseDown),
                new MissionOfferSourceDefinition(MissionOfferSourceKind.ServerEvent, MissionOfferSourceDefinition.MapArrivalKey, Tahrendra));
            Evidence(mission, "Headquarters radios it once Bringing the House Down is in, and on coming into the base; Captain Tillery " +
                "takes it in the Security Check Point room. Specialist Donnaway (package1633) waits at the wormhole room's door and " +
                "follows a player in; on the room's mark Dominator Ryxnl9489 comes through the main door with soldiers, and riflemen " +
                "on the balconies. She uploads the virus for three minutes; the second wave comes after one, the third after two, " +
                "each down its objective; the time up and the room clear, the upload is done, and the Holographic Eloh9078 " +
                "(package1635) has its recording. Donnaway takes no harm.");
            mission.Enable(scene);
        }

        private static InstanceMissionSql.RewardItem Item(uint template) => new(template, 1);

        private static MissionItemBinding Issued(string key, uint template) =>
            new(key, template, MissionItemScope.AssignmentIssued, 1,
                MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove, MissionItemCleanupDisposition.Remove);

        /// <summary>An unnamed marker of the objective: the n-th of its own.</summary>
        private static void Marker(InstanceMissionSql mission, MissionSceneDefinition scene, uint objective, uint n, Vector3 at, double radius)
        {
            var id = MissionIndicator.UnnamedFrom + objective * 10 + n;
            mission.Indicator(objective, id, at.X, at.Y, at.Z, radius);
            scene.IndicatorMaps ??= new Dictionary<uint, Dictionary<uint, uint>>();
            if (!scene.IndicatorMaps.TryGetValue(objective, out var maps))
                scene.IndicatorMaps[objective] = maps = new Dictionary<uint, uint>();
            maps[id] = Tahrendra;
        }

        private static MissionSceneDefinition Scene() => new()
        {
            Requirement = new CustomRequirement("character.starting-experience-completed"),
            Script = "data.sequence"
        };

        private static void Evidence(InstanceMissionSql mission, string reconstruction)
        {
            mission.Evidence(1, MissionEvidenceSourceKind.Client, "trpython:data/generated/client/missionobjective.pyo_dis",
                "Client1.16.5 mission, objective, text and conversation identities.");
            mission.Evidence(2, MissionEvidenceSourceKind.Documentation, Guide, "The walkthrough: people, places, order and rewards.");
            mission.Evidence(3, MissionEvidenceSourceKind.Documentation, Wiki, "The wiki: the Bane's levels and the missions' givers.");
            mission.Evidence(4, MissionEvidenceSourceKind.Reconstruction, "TahrendraBaseContent", reconstruction);
        }

        #endregion
    }
}
