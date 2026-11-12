using System.Collections.Generic;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// Damage across a wide level gap (LevelGap, the client's LEVELDIFF_DAMAGEMOD_*): full up to
    /// nine levels below the target, then 30% less a level, down to a tenth from twelve below.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class LevelGapTests
    {
        private const int Health = 1000;

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
        public void TheFactorIsTheClientsNinePercentSpreadFromNineLevelsToTwelve()
        {
            Assert.AreEqual(9, LevelGap.LevelMin, "LEVELDIFF_DAMAGEMOD_LEVEL_MIN");
            Assert.AreEqual(12, LevelGap.LevelMax, "LEVELDIFF_DAMAGEMOD_LEVEL_MAX");
            Assert.AreEqual(0.3, LevelGap.Modifier, 1e-9, "LEVELDIFF_DAMAGEMOD_MODIFIER: 0.9 / 3");

            foreach (var below in new[] { -20, -1, 0, 1, 5, 9 })
                Assert.AreEqual(1.0, LevelGap.Factor(below), $"{below} below");

            Assert.AreEqual(0.7, LevelGap.Factor(10), 1e-9);
            Assert.AreEqual(0.4, LevelGap.Factor(11), 1e-9);
            Assert.AreEqual(0.1, LevelGap.Factor(12), 1e-9);
            Assert.AreEqual(0.1, LevelGap.Factor(30), 1e-9, "no lower than a tenth");
        }

        [TestMethod]
        public void TheLowerAttackerDoesLessAndTheHigherOneIsUntouched()
        {
            var player = new Manifestation { Level = 5 };

            Assert.AreEqual(40, LevelGap.Scale(100, player, new Creature { Level = 16 }), "eleven below");
            Assert.AreEqual(100, LevelGap.Scale(100, player, new Creature { Level = 14 }), "nine below");
            Assert.AreEqual(100, LevelGap.Scale(100, new Creature { Level = 30 }, player), "a creature far above");
            Assert.AreEqual(10, LevelGap.Scale(100, new Creature { Level = 3 }, new Manifestation { Level = 15 }), "a creature twelve below");
            Assert.AreEqual(10, LevelGap.Scale(100, new Manifestation { Level = 1 }, new Manifestation { Level = 50 }), "a player on a player");
            Assert.AreEqual(1, LevelGap.Scale(1, player, new Creature { Level = 40 }), "a hit that did anything does 1");
            Assert.AreEqual(100, LevelGap.Scale(100, null, new Creature { Level = 40 }), "the world's own");
            Assert.AreEqual(100, LevelGap.Scale(100, new Creature { Level = 0 }, new Creature { Level = 40 }), "no level");
        }

        [TestMethod]
        public void AnAbilitysHitOnACreatureFarAboveDoesATenth()
        {
            using var world = new WorldTestContext();
            var player = world.CreateClient(0, 0).Player;
            player.Level = 1;
            var boss = Spawn(world, 13);

            Assert.AreEqual(10, ActorManager.Instance.Damage(world.Map, boss, 100, player, out var outcome, DamageType.Physical));
            Assert.AreEqual(10, outcome.Delivered, "shown as dealt");
            Assert.AreEqual(Health - 10, boss.Attributes[Attributes.Health].Current);

            var peer = Spawn(world, 10);
            Assert.AreEqual(100, ActorManager.Instance.Damage(world.Map, peer, 100, player, out _, DamageType.Physical), "nine above: in full");
        }

        [TestMethod]
        public void AShotAtACreatureFarAboveDoesLessAndShowsIt()
        {
            using var world = new WorldTestContext();
            var shooter = world.CreateClient(0, 0);
            shooter.Player.Level = 1;
            var target = Spawn(world, 12);
            var missile = new Missile
            {
                Source = shooter.Player,
                TargetActor = target,
                TargetEntityId = target.EntityId,
                DamageA = 100,
                DamageType = DamageType.Physical,
                ActionId = ActionId.WeaponAttack,
                ActionArgId = 1,
                CritChance = 0
            };

            MissileManager.Instance.MissileTrigger(world.Map, missile);

            Assert.AreEqual(Health - 40, target.Attributes[Attributes.Health].Current, "eleven above: 40%");
            Assert.AreEqual(40, missile.DamageA);
        }

        /// <summary>A hostile creature of the level, no armour, Health health, in the map's cells.</summary>
        private Creature Spawn(WorldTestContext world, uint level)
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
                Level = level,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>()
            };
            creature.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, Health, Health, Health, 0, 0);
            creature.Attributes[Attributes.Armor] = new ActorAttributes(Attributes.Armor, 0, 0, 0, 0, 0);
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
