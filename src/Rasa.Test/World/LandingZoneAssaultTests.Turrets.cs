using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Data;
using Rasa.Managers;
using Rasa.Services.Preloader;
using Rasa.Structures;

namespace Rasa.Test.World
{
    public partial class LandingZoneAssaultTests
    {
        /// <summary>
        /// The point taken by an assault, the way it goes in the game: the AFS turrets shot down to
        /// wrecks on their mounts, the rest of the garrison dead, the boss's claim. The Bane turrets
        /// stand on both gate bases and the three wall tops at once, nothing of the AFS is left on a
        /// mount, and nothing stands on the east wall's end cap (Landing_zone_fence_turret).
        /// </summary>
        [TestMethod]
        public void TakenByAnAssaultThePointGetsItsBaneTurretsOnBothGateBasesAndTheWallTops()
        {
            var gateBases = new (uint Pool, Vector3 At)[]
            {
                (551122, new Vector3(158.112f, 169.621f, -27.012f)),
                (551124, new Vector3(237.988f, 169.621f, -27.112f))
            };
            var endCap = new Vector3(254.74f, 172.4f, -110.69f);

            Assert.IsFalse(_world.Map.SpawnPools.Any(p => p.DbId == LandingZoneFenceTurret.AfsPool || p.DbId == LandingZoneFenceTurret.BanePool),
                "no pool on the end cap");
            Assert.IsFalse(Point.AfsPools.Contains(LandingZoneFenceTurret.AfsPool) || Point.BanePools.Contains(LandingZoneFenceTurret.BanePool),
                "nor a link to one");

            for (var i = 0; i < 2; i++) SpawnPoolManager.Instance.SpawnPoolWorker(_world.Map, 10_000_000);
            Deliver();
            Assert.AreEqual(5, AfsTurrets().Count(c => c.State != CharacterState.Dead), "the AFS turrets on their mounts");

            WaveThree();
            var boss = Troops.Single(c => c.DbId == LandingZoneAssaultContent.Boss);
            var troop = Troops.First(c => c != boss);
            boss.Position = NavMeshManager.NearestWalkable(_world.Map, Point.Position).Value;
            _world.MoveTo(boss.Position + new Vector3(0, 0, 8));

            // Shot down by the assault: wrecks on their mounts, kept for their pools.
            foreach (var turret in AfsTurrets().ToList())
            {
                turret.Attributes[Attributes.Health].Current = 0;
                CreatureManager.Instance.HandleCreatureKill(_world.Map, turret, troop);
            }
            Assert.AreEqual(5, AfsTurrets().Count(c => c.State == CharacterState.Dead), "five wrecks");

            ClearGarrison();
            ControlPoints.Instance.Worker(_world.Map);
            Assert.AreSame(boss, Point.Object.UsedBy, "the claim");
            _now += ControlPoints.Instance.Config.LandingZoneAssaultCaptureSeconds * 1000L;
            LandingZoneAssaults.Worker(_world.Map, Point);
            Assert.AreEqual(ControlPoints.Bane, Point.Owner);

            for (var i = 0; i < 3; i++)
            {
                ControlPoints.Instance.Worker(_world.Map);
                SpawnPoolManager.Instance.SpawnPoolWorker(_world.Map, 1000);
            }

            Assert.IsEmpty(AfsTurrets().ToList(), "the AFS wrecks are off their mounts");
            var bane = OnMap().Where(c => c.State != CharacterState.Dead && c.SpawnPool != null && Point.BanePools.Contains(c.SpawnPool.DbId)
                && Emplacements.Is(c)).ToList();
            foreach (var (pool, at) in gateBases)
            {
                var turret = bane.SingleOrDefault(c => c.SpawnPool.DbId == pool);
                Assert.IsNotNull(turret, $"the Bane turret of {pool} on its gate base");
                Assert.AreEqual(ControlPointTurretLoot.Creature, turret.DbId);
                Assert.IsLessThan(0.01f, Vector3.Distance(at, turret.Position), $"{pool} at {turret.Position}");
            }
            Assert.HasCount(5, bane, "two gate bases and three wall tops");
            Assert.IsFalse(OnMap().Any(c => Vector3.Distance(c.Position, endCap) < 1), "nothing on the end cap");
        }

        private System.Collections.Generic.IEnumerable<Creature> OnMap() =>
            _world.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Where(c => c != null).Distinct();

        private System.Collections.Generic.IEnumerable<Creature> AfsTurrets() =>
            OnMap().Where(c => c.SpawnPool != null && Point.AfsPools.Contains(c.SpawnPool.DbId) && Emplacements.Is(c));
    }
}
