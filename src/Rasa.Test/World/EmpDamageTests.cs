using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Data;
    using Rasa.Managers;
    using Rasa.Structures;

    /// <summary>
    /// EMP by the creature's kind (EmpDamage, the client's EMP_DAMAGE_MODIFIER_*): nothing to a
    /// BIOLOGICAL creature, half to a MACHINA, all of it to a MECHANICAL one, with its RESIST_ and
    /// VULNERABLE_ flags and the effects on it on top.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EmpDamageTests
    {
        /// <summary>No class: only the flags the test gives it.</summary>
        private static Creature With(params CreatureFlag[] flags)
        {
            var creature = new Creature { EntityClass = 0 };
            creature.ExtraFlags.AddRange(flags);
            return creature;
        }

        [TestMethod]
        public void EmpDoesNothingToTheLivingHalfToAMachinaAndAllToAMachine()
        {
            Assert.AreEqual(0, GameEffectManager.ApplyResist(With(CreatureFlag.Biological), 100, out var resisted, DamageType.EMP));
            Assert.AreEqual(100, resisted, "all of it resisted");

            Assert.AreEqual(50, GameEffectManager.ApplyResist(With(CreatureFlag.Machina), 100, out resisted, DamageType.EMP));
            Assert.AreEqual(50, resisted);

            Assert.AreEqual(100, GameEffectManager.ApplyResist(With(CreatureFlag.Mechanical), 100, out resisted, DamageType.EMP));
            Assert.AreEqual(0, resisted);

            Assert.AreEqual(100, GameEffectManager.ApplyResist(With(), 100, out _, DamageType.EMP), "of no kind: as before");
            Assert.AreEqual(100, GameEffectManager.ApplyResist(new Manifestation(), 100, out _, DamageType.EMP), "a player is no creature");
        }

        [TestMethod]
        public void OnlyEmpGoesByTheKind()
        {
            foreach (var type in new[] { DamageType.Physical, DamageType.Fire, DamageType.Ice, DamageType.Virulent, DamageType.Laser,
                         DamageType.Sonic, DamageType.Electrical, (DamageType)0 })
                Assert.AreEqual(100, GameEffectManager.ApplyResist(With(CreatureFlag.Biological), 100, out _, type), $"{type}");
        }

        [TestMethod]
        public void ResistanceAndVulnerabilityGoOnTopOfTheKind()
        {
            Assert.AreEqual(25, GameEffectManager.ApplyResist(With(CreatureFlag.Machina, CreatureFlag.ResistEmp), 100, out _, DamageType.EMP),
                "half for a Machina, half of that for its resistance");
            Assert.AreEqual(150, GameEffectManager.ApplyResist(With(CreatureFlag.Machina, CreatureFlag.VulnerableEmp), 100, out _, DamageType.EMP),
                "a vulnerable Machina is taken as a machine, and its vulnerability on top");
            Assert.AreEqual(150, GameEffectManager.ApplyResist(With(CreatureFlag.Mechanical, CreatureFlag.VulnerableEmp), 100, out _, DamageType.EMP));

            // The Bane Stalker: BIOLOGICAL and VULNERABLE_EMP. The vulnerability says EMP hurts it.
            Assert.AreEqual(150, GameEffectManager.ApplyResist(With(CreatureFlag.Biological, CreatureFlag.VulnerableEmp), 100, out var resisted, DamageType.EMP));
            Assert.AreEqual(-50, resisted);
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
    }
}
