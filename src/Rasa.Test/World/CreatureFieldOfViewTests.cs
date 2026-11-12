using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// FOV_LIMIT on a creature's ability (CreatureWindups.InFieldOfView): degrees either side of
    /// the way the creature faces that it reaches. A target that got round it, out of that arc,
    /// while it wound up has dodged it (CreatureWindups.StillCaught).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CreatureFieldOfViewTests
    {
        /// <summary>Facing -Z, as a rotation of 0 faces (AbilityManager.FacingOf).</summary>
        private static Creature Facing() => new Creature { Position = Vector3.Zero, Rotation = 0 };

        private static ActionLevelInfo Limit(int? degrees)
        {
            var info = new ActionLevelInfo { ActionId = (ActionId)412, Level = 1 };

            if (degrees.HasValue)
                info.Properties[AbilityProperty.FovLimit] = degrees.Value;

            return info;
        }

        [TestMethod]
        public void ATargetOutsideTheArcEitherSideOfItsFacingIsOutOfView()
        {
            var creature = Facing();

            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(0, 0, -5), Limit(90)), "straight ahead");
            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(5, 0, -0.5f), Limit(90)), "just in front of its side");
            Assert.IsFalse(CreatureWindups.InFieldOfView(creature, new Vector3(5, 0, 0.5f), Limit(90)), "just behind its side");
            Assert.IsFalse(CreatureWindups.InFieldOfView(creature, new Vector3(0, 0, 5), Limit(90)), "behind it");

            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(3, 0, -5), Limit(45)), "31 degrees off");
            Assert.IsFalse(CreatureWindups.InFieldOfView(creature, new Vector3(5, 0, -3), Limit(45)), "59 degrees off");
            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(5, 0, 1), Limit(120)), "a little behind its side, inside 120");

            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(0, 0, 5), Limit(null)), "no limit: all round");
            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(0, 0, 5), Limit(180)));
            Assert.IsTrue(CreatureWindups.InFieldOfView(creature, new Vector3(0, 10, 0.1f), Limit(45)), "standing on it counts as in front");
        }

        [TestMethod]
        public void TheClientsNineAttacksCarryTheirLimitAndALinkersHandBlastMissesWhoeverGotBehindIt()
        {
            using var harness = Rasa.Test.Missions.Wilderness.WildernessRuntimeTestHarness.Create();
            harness.LoadAbilities();

            var limits = new (ActionId Action, int Limit)[]
            {
                ((ActionId)200, 45), ((ActionId)243, 45), ((ActionId)426, 45), ((ActionId)428, 45), ((ActionId)497, 45),
                ((ActionId)264, 90), ((ActionId)395, 90), ((ActionId)412, 90), ((ActionId)263, 120)
            };

            foreach (var (action, limit) in limits)
            {
                Assert.IsTrue(AbilityManager.Instance.TryGetLevel(action, 1, out var info), $"{action}");
                Assert.AreEqual(limit, info.Get(AbilityProperty.FovLimit), $"{action}");
            }

            // CR_LINKER_HAND_BLAST: no area, FOV_LIMIT 90.
            var linker = Facing();
            var target = new Manifestation();
            var missile = new Missile { ActionId = (ActionId)412, ActionArgId = 1, TargetActor = target, Source = linker, AfterWindup = true };

            target.Position = new Vector3(1, 0, -6);
            Assert.IsTrue(CreatureWindups.StillCaught(linker, missile), "in front of it");

            target.Position = new Vector3(1, 0, 6);
            Assert.IsFalse(CreatureWindups.StillCaught(linker, missile), "got behind it: dodged");

            // An ability with no limit reaches all round, as before.
            var smash = new Missile { ActionId = ActionId.CrKaelRushingBlow, ActionArgId = 1, TargetActor = target, Source = linker, AfterWindup = true };
            Assert.IsTrue(AbilityManager.Instance.TryGetLevel(smash.ActionId, 1, out var rush));
            Assert.IsFalse(rush.Has(AbilityProperty.FovLimit));
            Assert.IsTrue(CreatureWindups.InFieldOfView(linker, target.Position, rush));
        }
    }
}
