using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// EMP by the creature's kind (EmpDamage, the client's EMP_DAMAGE_MODIFIER_*): none of a
    /// BIOLOGICAL creature's health, half a MACHINA's, all of a MECHANICAL one's - but shields and
    /// armour take it whole whatever the kind. And an EMP crit's Armor Suppression stops the
    /// target's armour regenerating for a while (CritEffects.SuppressArmor).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EmpDamageTests
    {
        private const int Health = 1000;
        private const int Armor = 50;

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

        /// <summary>No class: only the flags the test gives it.</summary>
        private static Creature With(params CreatureFlag[] flags)
        {
            var creature = new Creature { EntityClass = 0 };
            creature.ExtraFlags.AddRange(flags);
            return creature;
        }

        [TestMethod]
        public void EmpReachesNoneOfTheLivingsHealthHalfAMachinasAndAllAMachines()
        {
            Assert.AreEqual(0, EmpDamage.ToHealth(With(CreatureFlag.Biological), DamageType.EMP, 100));
            Assert.AreEqual(50, EmpDamage.ToHealth(With(CreatureFlag.Machina), DamageType.EMP, 100));
            Assert.AreEqual(100, EmpDamage.ToHealth(With(CreatureFlag.Mechanical), DamageType.EMP, 100));
            Assert.AreEqual(100, EmpDamage.ToHealth(With(), DamageType.EMP, 100), "of no kind: as before");
            Assert.AreEqual(100, EmpDamage.ToHealth(new Manifestation(), DamageType.EMP, 100), "a player is no creature");

            foreach (var type in new[] { DamageType.Physical, DamageType.Fire, DamageType.Ice, DamageType.Virulent, DamageType.Laser,
                         DamageType.Sonic, DamageType.Electrical, (DamageType)0 })
                Assert.AreEqual(100, EmpDamage.ToHealth(With(CreatureFlag.Biological), type, 100), $"{type}");
        }

        [TestMethod]
        public void TheKindIsNoResistanceButResistanceAndVulnerabilityStillCount()
        {
            Assert.AreEqual(100, GameEffectManager.ApplyResist(With(CreatureFlag.Biological), 100, out var resisted, DamageType.EMP));
            Assert.AreEqual(0, resisted, "the shields and armour of a living creature are hit in full");

            Assert.AreEqual(50, GameEffectManager.ApplyResist(With(CreatureFlag.Biological, CreatureFlag.ResistEmp), 100, out _, DamageType.EMP));
            Assert.AreEqual(150, GameEffectManager.ApplyResist(With(CreatureFlag.Machina, CreatureFlag.VulnerableEmp), 100, out _, DamageType.EMP));

            // The Bane Stalker: BIOLOGICAL and VULNERABLE_EMP. The vulnerability says EMP hurts it.
            Assert.AreEqual(EmpDamage.Mechanical, EmpDamage.FactorFor(With(CreatureFlag.Biological, CreatureFlag.VulnerableEmp), DamageType.EMP));
        }

        [TestMethod]
        public void ALivingCreaturesArmourIsStrippedByEmpAndItsHealthUntouched()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var thrax = Spawn(world, CreatureFlag.Biological);

            var taken = ActorManager.Instance.Damage(world.Map, thrax, 80, player, out var outcome, DamageType.EMP);

            Assert.AreEqual(Armor, taken, "the armour, all of it");
            Assert.AreEqual(Armor, outcome.Delivered, "what is shown is what it took");
            Assert.AreEqual(0, thrax.Attributes[Attributes.Armor].Current);
            Assert.AreEqual(Health, thrax.Attributes[Attributes.Health].Current);

            // With its armour gone, EMP does it nothing.
            Assert.AreEqual(0, ActorManager.Instance.Damage(world.Map, thrax, 80, player, out outcome, DamageType.EMP));
            Assert.AreEqual(0, outcome.Delivered);
            Assert.AreEqual(Health, thrax.Attributes[Attributes.Health].Current);

            // Any other type goes to its health.
            Assert.AreEqual(80, ActorManager.Instance.Damage(world.Map, thrax, 80, player, out _, DamageType.Physical));
            Assert.AreEqual(Health - 80, thrax.Attributes[Attributes.Health].Current);
        }

        [TestMethod]
        public void AMachinaTakesHalfPastItsArmourAndAMachineAllOfIt()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var machina = Spawn(world, CreatureFlag.Machina);
            var predator = Spawn(world, CreatureFlag.Mechanical);

            Assert.AreEqual(Armor + 15, ActorManager.Instance.Damage(world.Map, machina, 80, player, out var outcome, DamageType.EMP));
            Assert.AreEqual(Armor + 15, outcome.Delivered);
            Assert.AreEqual(Health - 15, machina.Attributes[Attributes.Health].Current);

            Assert.AreEqual(80, ActorManager.Instance.Damage(world.Map, predator, 80, player, out outcome, DamageType.EMP));
            Assert.AreEqual(80, outcome.Delivered);
            Assert.AreEqual(Health - 30, predator.Attributes[Attributes.Health].Current);
        }

        [TestMethod]
        public void AShieldOnALivingCreatureTakesItsShareOfEmp()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var thrax = Spawn(world, CreatureFlag.Biological);
            var shield = new GameEffect
            {
                TypeId = 77,
                EffectId = GameEffectManager.Instance.NextEffectId(world.Map),
                IsBuff = true,
                AbsorbPercent = 50,
                AbsorbPool = new AbsorbPool { Remaining = 500 },
                ExpiresTick = Environment.TickCount64 + 60000
            };
            thrax.ActiveEffects[shield.EffectId] = shield;

            ActorManager.Instance.Damage(world.Map, thrax, 100, player, out var outcome, DamageType.EMP);

            Assert.AreEqual(50, outcome.Absorbed, "the shield's half");
            Assert.AreEqual(450, shield.AbsorbPool.Remaining);
            Assert.AreEqual(0, thrax.Attributes[Attributes.Armor].Current, "the armour the rest");
            Assert.AreEqual(Health, thrax.Attributes[Attributes.Health].Current);
        }

        [TestMethod]
        public void AnEmpShotStripsALivingCreaturesArmourAndTheHitShowsOnlyThat()
        {
            using var world = new WorldTestContext();
            var shooter = world.CreateClient(0, 0);
            var thrax = Spawn(world, CreatureFlag.Biological);
            var missile = new Missile
            {
                Source = shooter.Player,
                TargetActor = thrax,
                TargetEntityId = thrax.EntityId,
                DamageA = 80,
                DamageType = DamageType.EMP,
                ActionId = ActionId.WeaponAttack,
                ActionArgId = 1,
                CritChance = 0
            };

            MissileManager.Instance.MissileTrigger(world.Map, missile);

            Assert.AreEqual(0, thrax.Attributes[Attributes.Armor].Current);
            Assert.AreEqual(Health, thrax.Attributes[Attributes.Health].Current);
            Assert.AreEqual(Armor, missile.DamageA, "what landed is the armour");
        }

        [TestMethod]
        public void AnEmpCritStopsTheArmourRegeneratingUntilItComesOff()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            var thrax = Spawn(world, CreatureFlag.Mechanical);
            var armor = thrax.Attributes[Attributes.Armor];

            armor.Current = 10;
            CreatureArmor.SyncRate(world.Map, thrax, armor);
            Assert.IsGreaterThan(0, GameEffectManager.RegenAmount(thrax, armor));

            CritEffects.SuppressArmor(world.Map, thrax, player);

            Assert.AreEqual(0, GameEffectManager.RegenAmount(thrax, armor), "suppressed");
            for (var second = 1; second <= 30; second++)
                CreatureArmor.Tick(thrax, armor, second);
            Assert.AreEqual(10, armor.Current, "no regeneration while it is on");

            // The armour still stops hits: it is its regeneration that is suppressed.
            Assert.AreEqual(10, ActorManager.Instance.Damage(world.Map, thrax, 10, player, out _, DamageType.Physical));
            Assert.AreEqual(0, armor.Current);
            Assert.AreEqual(Health, thrax.Attributes[Attributes.Health].Current);

            // Off again, it regenerates.
            foreach (var effect in new List<GameEffect>(thrax.ActiveEffects.Values))
                if (effect.TypeId == CritEffects.CritEmpTypeId)
                    GameEffectManager.Instance.DettachEffect(world.Map, thrax, effect);

            Assert.IsGreaterThan(0, GameEffectManager.RegenAmount(thrax, armor));
        }

        [TestMethod]
        public void TheClientsClassesSayWhichKindEachIs()
        {
            using var harness = Rasa.Test.Missions.Wilderness.WildernessRuntimeTestHarness.Create();

            // Bane_Thrax_Soldier_Rifle is BIOLOGICAL, Bane_Hominis_Machina MACHINA, Vehicle_Bane_Predator MECHANICAL.
            Assert.AreEqual(EmpDamage.Biological, EmpDamage.FactorFor(new Creature { EntityClass = (EntityClasses)4047 }, DamageType.EMP));
            Assert.AreEqual(EmpDamage.Machina, EmpDamage.FactorFor(new Creature { EntityClass = (EntityClasses)3868 }, DamageType.EMP));
            Assert.AreEqual(EmpDamage.Mechanical, EmpDamage.FactorFor(new Creature { EntityClass = (EntityClasses)3902 }, DamageType.EMP));
            Assert.AreEqual(EmpDamage.Mechanical, EmpDamage.FactorFor(new Creature { EntityClass = (EntityClasses)3781 }, DamageType.EMP),
                "Vehicle_Bane_Stalker: BIOLOGICAL, and VULNERABLE_EMP");
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
