using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rasa.Config;
using Rasa.Managers;
using Rasa.Structures;
using Rasa.Structures.World;
using Rasa.Test.Missions.Wilderness;

namespace Rasa.Test.World
{
    /// <summary>
    /// A point's defenders alerted (ControlPoints.Alerts, ControlPoints.CaptureAlarm), on the
    /// Wilderness's own points: Imperial Valley given to the Bane, whose Thrax stand by the point
    /// and whose two mortars stand some fifty metres off; and the Landing Zone, the AFS's.
    /// </summary>
    [TestClass, DoNotParallelize]
    public class ControlPointAlertTests
    {
        private const uint ImperialValley = 3, LandingZone = 4;
        private const uint ThraxTechnicians = 580029;

        private WildernessRuntimeTestHarness _world;
        private ControlPointConfig _config;

        private static ControlPoints.Point Point(uint id) => ControlPoints.Instance.ById(id);

        private Manifestation Player => _world.Client.Player;

        private ControlPointConfig Config => ControlPoints.Instance.Config;

        [TestInitialize]
        public void Start()
        {
            _config = ControlPoints.Instance.Config;
            ControlPoints.Instance.Config = new ControlPointConfig { LandingZoneAssaultEnabled = false };
            _world = WildernessRuntimeTestHarness.Create();
            using var db = _world.CreateWorld();
            ControlPoints.Instance.Load(db.ControlPoints.GetControlPoints().Where(p => p.Id == ImperialValley || p.Id == LandingZone),
                db.ControlPoints.GetLinks().Where(l => l.ControlPointId == ImperialValley || l.ControlPointId == LandingZone), null);
            ControlPoints.Instance.Place(_world.Map);
        }

        [TestCleanup]
        public void End()
        {
            ControlPoints.Instance.Load(Array.Empty<ControlPointEntry>(), Array.Empty<ControlPointLinkEntry>(), null);
            _world.Dispose();
            ControlPoints.Instance.Config = _config;
        }

        [TestMethod]
        public void APlayerComingInSetsTheDefendersInReachOnThemOnceForEachTimeTheyComeIn()
        {
            var point = BaneValley();
            var garrison = ControlPoints.Instance.Defenders(_world.Map, point);
            var at = point.Object.Position;
            Assert.IsTrue(garrison.Any(), "the Bane garrison stands");

            _world.MoveTo(at + new Vector3(0, 0, -200));
            Pass();
            Assert.IsTrue(garrison.All(d => d.Hate.Of(Player.EntityId) == 0), "nobody alerted from outside");

            var spot = at + new Vector3(0, 0, -(Config.DefenderAlertRadius - 2));
            _world.MoveTo(spot);
            Pass();
            var inReach = garrison.Where(d => Flat(d.Position, spot) <= BehaviorManager.MaxChaseDistance).ToList();
            var beyond = garrison.Except(inReach).ToList();
            Assert.IsTrue(inReach.Any(), "the Thrax by the point");
            Assert.IsTrue(beyond.Any(), "a mortar beyond the leash");
            foreach (var defender in inReach)
            {
                Assert.AreEqual(Config.DefenderAlertHate, defender.Hate.Of(Player.EntityId), $"{defender.DbId}");
                Assert.AreEqual(Player.EntityId, defender.Controller.ActionFighting.TargetEntityId, $"{defender.DbId} turned on them");
            }
            foreach (var defender in beyond)
                Assert.AreEqual(0, defender.Hate.Of(Player.EntityId), $"{defender.DbId} is beyond its leash");

            Pass();
            _world.MoveTo(at + new Vector3(0, 0, -(Config.DefenderAlertRadius + ControlPoints.AlertExitMargin - 1)));
            Pass();
            foreach (var defender in inReach)
                Assert.AreEqual(Config.DefenderAlertHate, defender.Hate.Of(Player.EntityId), "staying in, and at the edge, is no new alert");

            _world.MoveTo(at + new Vector3(0, 0, -(Config.DefenderAlertRadius + ControlPoints.AlertExitMargin + 10)));
            Pass();
            _world.MoveTo(spot);
            Pass();
            foreach (var defender in inReach)
                Assert.AreEqual(2.0 * Config.DefenderAlertHate, defender.Hate.Of(Player.EntityId), "out and back in again");
        }

        [TestMethod]
        public void NobodyIsSetOnAPlayerTheyCannotSeeOrWouldNotGoAfter()
        {
            var point = BaneValley();
            var garrison = ControlPoints.Instance.Defenders(_world.Map, point);
            var spot = point.Object.Position + new Vector3(0, 0, -10);

            Player.GmFlagAlwaysFriendly = true;
            _world.MoveTo(spot);
            Pass();
            Assert.IsTrue(garrison.All(d => d.Hate.Of(Player.EntityId) == 0), "an always-friendly game master");
            Player.GmFlagAlwaysFriendly = false;
            _world.MoveTo(spot + new Vector3(0, 0, -200));
            Pass();

            Player.GmUntargetable = true;
            _world.MoveTo(spot);
            Pass();
            Assert.IsTrue(garrison.All(d => d.Hate.Of(Player.EntityId) == 0), "an untargetable game master");
            Player.GmUntargetable = false;
            _world.MoveTo(spot + new Vector3(0, 0, -200));
            Pass();

            // The AFS's own garrison does not go after a player.
            var landingZone = Point(LandingZone);
            SpawnAfsLandingZone();
            var afs = ControlPoints.Instance.Defenders(_world.Map, landingZone);
            Assert.IsTrue(afs.Any());
            _world.MoveTo(landingZone.Object.Position + new Vector3(5, 0, 5));
            Pass();
            Assert.IsTrue(afs.All(d => d.Hate.Of(Player.EntityId) == 0), "the AFS garrison and a player");
        }

        [TestMethod]
        public void TheAfsDefendersAreSetOnABaneCreatureComingInAndTheVendorsAreNot()
        {
            var point = Point(LandingZone);
            SpawnAfsLandingZone();
            _world.SpawnWorldAfter(10_000_000, ThraxTechnicians);
            // The camp scatters its Thrax about it: they are all put well outside the radius first.
            var camp = Living().Where(c => c.SpawnPool?.DbId == ThraxTechnicians).ToList();
            var thrax = camp.First();
            foreach (var one in camp)
                Put(one, point.Object.Position + new Vector3(0, 0, 150));
            var afs = ControlPoints.Instance.Defenders(_world.Map, point);
            Pass();
            Assert.IsTrue(afs.All(d => d.Hate.Of(thrax.EntityId) == 0), "outside the radius");

            var spot = point.Object.Position + new Vector3(4, 0, 4);
            Put(thrax, spot);
            Pass();

            var inReach = afs.Where(d => ControlPoints.Seeks(d, thrax) && Flat(d.Position, spot) <= BehaviorManager.MaxChaseDistance).ToList();
            Assert.IsTrue(inReach.Any());
            foreach (var defender in afs)
                Assert.AreEqual(inReach.Contains(defender) ? Config.DefenderAlertHate : 0, defender.Hate.Of(thrax.EntityId), $"{defender.DbId}");

            var vendors = Living().Where(c => c.SpawnPool != null && point.AfsPools.Contains(c.SpawnPool.DbId)
                && ControlPoints.Instance.ServicePointOf(_world.Map, c.SpawnPool) != null).ToList();
            Assert.IsTrue(vendors.All(v => v.Hate.Of(thrax.EntityId) == 0), "service NPCs are not defenders");
        }

        [TestMethod]
        public void ARadiusOfNothingAlertsNobody()
        {
            Config.DefenderAlertRadius = 0;
            var point = BaneValley();
            var garrison = ControlPoints.Instance.Defenders(_world.Map, point);

            _world.MoveTo(point.Object.Position + new Vector3(0, 0, -5));
            Pass();

            Assert.IsTrue(garrison.All(d => d.Hate.Of(Player.EntityId) == 0));
        }

        [TestMethod]
        public void BeginningACaptureRaisesTheAlarmOnTheClaimant()
        {
            var point = BaneValley();
            var garrison = ControlPoints.Instance.Defenders(_world.Map, point);
            var spot = point.Object.Position + new Vector3(0, 0, -5);
            Config.DefenderAlertRadius = 0;
            _world.MoveTo(spot);

            ControlPoints.Instance.CaptureAlarm(_world.Map, point, Player);

            var inReach = garrison.Where(d => Flat(d.Position, spot) <= BehaviorManager.MaxChaseDistance).ToList();
            Assert.IsTrue(inReach.Any());
            foreach (var defender in garrison)
                Assert.AreEqual(inReach.Contains(defender) ? Config.CaptureAlarmHate : 0, defender.Hate.Of(Player.EntityId), $"{defender.DbId}");
        }

        /// <summary>Imperial Valley given to the Bane and its garrison set down.</summary>
        private ControlPoints.Point BaneValley()
        {
            var point = Point(ImperialValley);
            ControlPoints.Instance.SetOwner(point, ControlPoints.Bane, null);
            _world.SpawnWorldAfter(10_000_000, point.BanePools.ToArray());
            Deliver();
            return point;
        }

        private void SpawnAfsLandingZone()
        {
            var pools = Point(LandingZone).AfsPools.Where(id => _world.Map.SpawnPools.Any(p => p.DbId == id)).ToArray();
            for (var i = 0; i < 2; i++)
                _world.SpawnWorldAfter(10_000_000, pools);
            Deliver();
        }

        private void Deliver()
        {
            for (var i = 0; i < 6; i++)
                DynamicObjectManager.Instance.DropshipsWorker(_world.Map, 60000);
        }

        private void Pass() => ControlPoints.Instance.Worker(_world.Map);

        private IEnumerable<Creature> Living() =>
            _world.Map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct().Where(c => CreatureManager.IsLivingOnMap(_world.Map, c)).ToList();

        /// <summary>The creature moved, and into the cell it is now in.</summary>
        private void Put(Creature creature, Vector3 position)
        {
            creature.Position = position;
            CreatureManager.Instance.CellUpdateLocation(_world.Map, creature,
                (uint)(position.X / CellManager.CellSize + CellManager.CellBias), (uint)(position.Z / CellManager.CellSize + CellManager.CellBias));
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
    }
}
