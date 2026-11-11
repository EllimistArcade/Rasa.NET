using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// Virulent (VirulentDamage): past the armour bar straight to health, held off by the armour
    /// the target has left - VIRULENT_RESIST_FROM_ARMOR_MOD, 75% at full armour - and nothing to
    /// a machine.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class VirulentDamageTests
    {
        private const int Health = 1000;
        private const int Armor = 80;

        private readonly List<Creature> _creatures = new List<Creature>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var creature in _creatures)
            {
                EntityManager.Instance.UnregisterCreature(creature.EntityId);
                EntityManager.Instance.UnregisterActor(creature.EntityId);
                EntityManager.Instance.UnregisterEntity(creature.EntityId);
            }

            _creatures.Clear();
        }

        [TestMethod]
        public void TheArmourLeftHoldsOffUpToThreeQuartersOfIt()
        {
            Assert.AreEqual(0.75, VirulentDamage.ResistFromArmorMod, "VIRULENT_RESIST_FROM_ARMOR_MOD");

            Assert.AreEqual(0.25, VirulentDamage.ArmorFactor(Armoured(80, 80), DamageType.Virulent), 1e-9, "full armour");
            Assert.AreEqual(0.625, VirulentDamage.ArmorFactor(Armoured(40, 80), DamageType.Virulent), 1e-9, "half");
            Assert.AreEqual(1.0, VirulentDamage.ArmorFactor(Armoured(0, 80), DamageType.Virulent), "stripped");
            Assert.AreEqual(1.0, VirulentDamage.ArmorFactor(new Creature(), DamageType.Virulent), "no armour at all");
            Assert.AreEqual(1.0, VirulentDamage.ArmorFactor(Armoured(0, 0), DamageType.Virulent));

            foreach (var type in new[] { DamageType.Physical, DamageType.Fire, DamageType.Ice, DamageType.EMP, DamageType.Laser,
                         DamageType.Sonic, DamageType.Electrical, (DamageType)0 })
                Assert.AreEqual(1.0, VirulentDamage.ArmorFactor(Armoured(80, 80), type), $"{type}");
        }

        [TestMethod]
        public void WhatTheArmourHoldsOffIsResistedWithTheTargetsOwnResistance()
        {
            Assert.AreEqual(25, GameEffectManager.ApplyResist(Armoured(80, 80), 100, out var resisted, DamageType.Virulent));
            Assert.AreEqual(75, resisted);

            Assert.AreEqual(100, GameEffectManager.ApplyResist(Armoured(80, 80), 100, out resisted, DamageType.Physical));
            Assert.AreEqual(0, resisted);

            // RESIST_VIRULENT halves it, and the armour left takes its share of the rest.
            var resistant = Armoured(40, 80);
            resistant.ExtraFlags.Add(CreatureFlag.ResistVirulent);
            Assert.AreEqual(31, GameEffectManager.ApplyResist(resistant, 100, out _, DamageType.Virulent), "100 x 0.5 x 0.625");
        }

        [TestMethod]
        public void ItGoesPastTheArmourBarToHealthAndHitsHarderOnceTheArmourIsStripped()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var thrax = Spawn(world, CreatureFlag.Biological);

            Assert.AreEqual(25, Hit(world, player, thrax, 100, DamageType.Virulent));
            Assert.AreEqual(Armor, thrax.Attributes[Attributes.Armor].Current, "the armour bar untouched");
            Assert.AreEqual(Health - 25, thrax.Attributes[Attributes.Health].Current);

            // Physical takes the armour first; with it gone, Virulent lands whole.
            Assert.AreEqual(Armor, Hit(world, player, thrax, Armor, DamageType.Physical));
            Assert.AreEqual(0, thrax.Attributes[Attributes.Armor].Current);
            Assert.AreEqual(Health - 25, thrax.Attributes[Attributes.Health].Current);

            Assert.AreEqual(100, Hit(world, player, thrax, 100, DamageType.Virulent));
            Assert.AreEqual(Health - 125, thrax.Attributes[Attributes.Health].Current);
        }

        [TestMethod]
        public void AVirulentShotGoesPastTheArmourAndShowsWhatWasHeldOffAsResisted()
        {
            using var world = new WorldTestContext();
            var shooter = world.CreateClient(0, 0);
            var thrax = Spawn(world, CreatureFlag.Biological);
            var missile = new Missile
            {
                Source = shooter.Player,
                TargetActor = thrax,
                TargetEntityId = thrax.EntityId,
                DamageA = 100,
                DamageType = DamageType.Virulent,
                ActionId = ActionId.WeaponAttack,
                ActionArgId = 1,
                CritChance = 0
            };

            MissileManager.Instance.MissileTrigger(world.Map, missile);

            Assert.AreEqual(Armor, thrax.Attributes[Attributes.Armor].Current);
            Assert.AreEqual(Health - 25, thrax.Attributes[Attributes.Health].Current);
            Assert.AreEqual(25, missile.DamageA);
        }

        [TestMethod]
        public void APlayersArmourBarIsPassedOverToo()
        {
            using var world = new WorldTestContext();
            var attacker = Spawn(world, CreatureFlag.Biological);
            var player = world.CreateClient(0, 0).Player;
            player.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, Health, Health, Health, 0, 0);
            player.Attributes[Attributes.Armor] = new ActorAttributes(Attributes.Armor, Armor, Armor, 0, 0, 0);

            Assert.AreEqual(40, Hit(world, attacker, player, 40, DamageType.Virulent), "no armour left: all of it");
            Assert.AreEqual(Health - 40, player.Attributes[Attributes.Health].Current);
            Assert.AreEqual(0, player.Attributes[Attributes.Armor].Current);

            player.Attributes[Attributes.Armor].Current = Armor;
            Assert.AreEqual(10, Hit(world, attacker, player, 40, DamageType.Virulent), "full armour: a quarter");
            Assert.AreEqual(Armor, player.Attributes[Attributes.Armor].Current);
        }

        [TestMethod]
        public void MachinesAreImmuneButAVulnerableOneAndAMachinaAreNot()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var turret = Spawn(world, CreatureFlag.Mechanical);

            Assert.IsTrue(DamageImmunity.IsImmune(turret, DamageType.Virulent));
            Assert.AreEqual(0, ActorManager.Instance.Damage(world.Map, turret, 100, player, out var outcome, DamageType.Virulent));
            Assert.IsTrue(outcome.Immune, "shown Immune");
            Assert.AreEqual(Health, turret.Attributes[Attributes.Health].Current);
            Assert.IsFalse(DamageImmunity.IsImmune(turret, DamageType.Physical));

            // The Bane Linker: MECHANICAL and VULNERABLE_VIRULENT.
            var linker = Spawn(world, CreatureFlag.Mechanical);
            linker.ExtraFlags.Add(CreatureFlag.VulnerableVirulent);
            Assert.IsFalse(DamageImmunity.IsImmune(linker, DamageType.Virulent));

            Assert.IsFalse(DamageImmunity.IsImmune(Spawn(world, CreatureFlag.Machina), DamageType.Virulent));
            Assert.IsFalse(DamageImmunity.IsImmune(Spawn(world, CreatureFlag.Biological), DamageType.Virulent));
        }

        [TestMethod]
        public void TheClientsMachinesAreImmune()
        {
            using var harness = Rasa.Test.Missions.Wilderness.WildernessRuntimeTestHarness.Create();

            // Vehicle_Bane_Predator (RESIST_VIRULENT) and Emplacement_Bane_Turret_Standard (no flag for it) are MECHANICAL;
            // Bane_Thrax_Soldier_Rifle is BIOLOGICAL.
            Assert.IsTrue(DamageImmunity.IsImmune(new Creature { EntityClass = (EntityClasses)3902 }, DamageType.Virulent));
            Assert.IsFalse(DamageImmunity.IsImmune(new Creature { EntityClass = (EntityClasses)4047 }, DamageType.Virulent));
        }

        /// <summary>Resisted, then dealt, as an ability's hit is.</summary>
        private static int Hit(WorldTestContext world, Actor source, Actor target, int amount, DamageType type) =>
            ActorManager.Instance.Damage(world.Map, target, GameEffectManager.ApplyResist(target, amount, out _, type), source, out _, type);

        private static Creature Armoured(int current, int max)
        {
            var creature = new Creature { EntityClass = 0 };
            creature.Attributes[Attributes.Armor] = new ActorAttributes(Attributes.Armor, max, max, current, 0, 0);
            return creature;
        }

        /// <summary>A hostile creature of the kind, no class, Health health and Armor armour, in the map's cells.</summary>
        private Creature Spawn(WorldTestContext world, CreatureFlag kind)
        {
            var map = world.Map;
            var creature = new Creature
            {
                Name = "Fixture",
                TargetCategory = TargetCategory.Hostile,
                MapContextId = map.MapInfo.MapContextId,
                RuntimeMapChannel = map,
                Position = new Vector3(5, 0, 5),
                EntityClass = 0,
                State = CharacterState.Idle,
                Level = 1,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>()
            };
            creature.ExtraFlags.Add(kind);
            creature.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, Health, Health, Health, 0, 0);
            creature.Attributes[Attributes.Armor] = new ActorAttributes(Attributes.Armor, Armor, Armor, Armor, 0, 0);
            EntityManager.Instance.RegisterEntity(creature.EntityId, EntityType.Creature);
            EntityManager.Instance.RegisterCreature(creature);
            EntityManager.Instance.RegisterActor(creature.EntityId, creature);

            var seed = CellManager.Instance.GetCellSeed(creature.Position);
            creature.Cells = CellManager.Instance.CreateCellMatrix(map, seed & 0xFFFF, seed >> 16);
            CellManager.Instance.GetCell(map, seed & 0xFFFF, seed >> 16).CreatureList.Add(creature);

            _creatures.Add(creature);
            return creature;
        }
    }
}
