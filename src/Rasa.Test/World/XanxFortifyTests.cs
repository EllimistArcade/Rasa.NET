using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// A Xanx's meal (CR_XANX_DEVOUR) is XANX_FORTIFY: for the argument's DURATION it raises the
    /// Xanx's maximum health by the heal amount, and its health with it (CreatureHabits.Fortify).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class XanxFortifyTests
    {
        private Creature _xanx;

        [TestCleanup]
        public void Cleanup()
        {
            if (_xanx == null)
                return;

            EntityManager.Instance.UnregisterCreature(_xanx.EntityId);
            EntityManager.Instance.UnregisterActor(_xanx.EntityId);
            EntityManager.Instance.UnregisterEntity(_xanx.EntityId);
            _xanx = null;
        }

        [TestMethod]
        public void AMealRaisesTheXanxsMaximumAndItsHealthForFiveMinutesAndNoMore()
        {
            using var world = new WorldTestContext();
            var xanx = Xanx(world, 400);
            var info = Devour();
            var amount = CreatureHabits.MealAmount(xanx, info);

            Assert.IsGreaterThan(0, amount);

            CreatureHabits.Fortify(world.Map, xanx, info);

            var health = xanx.Attributes[Attributes.Health];
            var fortify = xanx.ActiveEffects.Values.Single(e => e.TypeId == CreatureHabits.XanxFortifyTypeId);

            Assert.AreEqual(1000 + amount, health.CurrentMax, "its maximum raised by the meal");
            Assert.AreEqual(400 + amount, health.Current, "and its health with it: what it regains");
            Assert.IsTrue(fortify.ExpiresTick - Environment.TickCount64 is > 299000 and <= 300000, "DURATION 300 s");

            // A second meal replaces the first: one raise, not two.
            CreatureHabits.Fortify(world.Map, xanx, info);
            Assert.AreEqual(1000 + amount, health.CurrentMax);
            Assert.AreEqual(1, xanx.ActiveEffects.Values.Count(e => e.TypeId == CreatureHabits.XanxFortifyTypeId));

            // When it ends the maximum goes back, and the health is capped to it.
            var last = xanx.ActiveEffects.Values.Single(e => e.TypeId == CreatureHabits.XanxFortifyTypeId);
            GameEffectManager.Instance.DettachEffect(world.Map, xanx, last);

            Assert.AreEqual(1000, health.CurrentMax);
            Assert.IsTrue(health.Current <= 1000);
        }

        [TestMethod]
        public void AXanxWhoseHealingIsBlockedGainsNothingFromAMeal()
        {
            using var world = new WorldTestContext();
            var xanx = Xanx(world, 400);

            xanx.ActiveEffects[999001] = new GameEffect
            {
                TypeId = 1, EffectId = 999001, BlocksHealing = true, IsBuff = false,
                ExpiresTick = Environment.TickCount64 + 60000
            };

            CreatureHabits.Fortify(world.Map, xanx, Devour());

            Assert.AreEqual(1000, xanx.Attributes[Attributes.Health].CurrentMax);
            Assert.AreEqual(400, xanx.Attributes[Attributes.Health].Current);
            Assert.IsFalse(xanx.ActiveEffects.Values.Any(e => e.TypeId == CreatureHabits.XanxFortifyTypeId));
        }

        /// <summary>CR_XANX_DEVOUR argument 1 as the world database has it.</summary>
        private static ActionLevelInfo Devour()
        {
            var info = new ActionLevelInfo { ActionId = CreatureHabits.XanxDevour, Level = 1, WindupMs = 1000, RecoveryMs = 5300 };
            info.Properties[AbilityProperty.Duration] = 300;
            info.Properties[AbilityProperty.HealAmountMin] = 1000;
            info.Properties[AbilityProperty.HealAmountMax] = 1000;
            info.Properties[AbilityProperty.DamageScaleType] = 2;
            return info;
        }

        private Creature Xanx(WorldTestContext world, int health)
        {
            _xanx = new Creature
            {
                Name = "Xanx",
                TargetCategory = TargetCategory.Hostile,
                MapContextId = world.Map.MapInfo.MapContextId,
                RuntimeMapChannel = world.Map,
                Position = Vector3.Zero,
                EntityClass = EntityClasses.HumanBaseMale,
                State = CharacterState.Idle,
                Level = 10,
                AppearanceData = new Dictionary<EquipmentData, AppearanceData>()
            };
            _xanx.Attributes[Attributes.Health] = new ActorAttributes(Attributes.Health, 1000, 1000, health, 0, 0);
            EntityManager.Instance.RegisterEntity(_xanx.EntityId, EntityType.Creature);
            EntityManager.Instance.RegisterCreature(_xanx);
            EntityManager.Instance.RegisterActor(_xanx.EntityId, _xanx);
            var seed = CellManager.Instance.GetCellSeed(_xanx.Position);
            _xanx.Cells = CellManager.Instance.CreateCellMatrix(world.Map, seed & 0xFFFF, seed >> 16);
            CellManager.Instance.GetCell(world.Map, seed & 0xFFFF, seed >> 16).CreatureList.Add(_xanx);
            return _xanx;
        }
    }
}
