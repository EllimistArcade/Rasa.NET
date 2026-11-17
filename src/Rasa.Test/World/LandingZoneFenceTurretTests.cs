using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Services.Preloader;
    using Rasa.Structures.World;
    using Rasa.Test.Database;

    /// <summary>
    /// The Landing Zone's turret pair on the east wall's end cap, where no gun base is, taken off
    /// (Landing_zone_fence_turret, LandingZoneFenceTurret).
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class LandingZoneFenceTurretTests
    {
        private const string Migration = "20271201000000_Landing_zone_fence_turret";
        private const string Previous = "20271130000000_Guardian_prominence";

        [TestMethod]
        public void UpTakesThePairAndItsLinksOffAndDownPutsThemBackAsTheyWere()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            var poolsBefore = Pools(database);
            var linksBefore = Links(database);
            var afs = poolsBefore[LandingZoneFenceTurret.AfsPool];
            var bane = poolsBefore[LandingZoneFenceTurret.BanePool];
            Assert.AreEqual((254.74, 172.4, -110.69, -0.7895, 1220U, 550018U, (byte)0), (afs.PosX, afs.PosY, afs.PosZ, afs.Rotation, afs.MapContextId, afs.Creature1Id, afs.Mode));
            Assert.AreEqual((254.74, 172.4, -110.69, -0.7895, 1220U, 551001U, (byte)1), (bane.PosX, bane.PosY, bane.PosZ, bane.Rotation, bane.MapContextId, bane.Creature1Id, bane.Mode));
            CollectionAssert.Contains(linksBefore, (4U, ControlPointLinkEntry.KindAfsPool, LandingZoneFenceTurret.AfsPool));
            CollectionAssert.Contains(linksBefore, (4U, ControlPointLinkEntry.KindBanePool, LandingZoneFenceTurret.BanePool));

            migrator.Migrate(Migration);
            var pools = Pools(database);
            var links = Links(database);
            CollectionAssert.AreEquivalent(poolsBefore.Keys.Except(new[] { LandingZoneFenceTurret.AfsPool, LandingZoneFenceTurret.BanePool }).ToList(),
                pools.Keys.ToList(), "the pair, and nothing else");
            CollectionAssert.AreEquivalent(linksBefore.Where(l => l.ObjectId != LandingZoneFenceTurret.AfsPool && l.ObjectId != LandingZoneFenceTurret.BanePool).ToList(),
                links, "their links, and nothing else");
            Assert.AreEqual(3, links.Count(l => l.Point == 4 && l.Kind == ControlPointLinkEntry.KindAfsPool && pools[l.ObjectId].PosY > 172),
                "the three wall tops on the corners stay");

            foreach (var sql in LandingZoneFenceTurret.Up())
                database.Context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
            CollectionAssert.AreEquivalent(pools.Keys.ToList(), Pools(database).Keys.ToList(), "twice");

            migrator.Migrate(Previous);
            var poolsAfter = Pools(database);
            CollectionAssert.AreEquivalent(poolsBefore.Keys.ToList(), poolsAfter.Keys.ToList());
            foreach (var id in new[] { LandingZoneFenceTurret.AfsPool, LandingZoneFenceTurret.BanePool })
                Assert.AreEqual(Row(poolsBefore[id]), Row(poolsAfter[id]), $"{id} as it was");
            CollectionAssert.AreEquivalent(linksBefore, Links(database), "and the links");
        }

        [TestMethod]
        public void APoolAGmMovedStays()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();

            migrator.Migrate(Previous);
            database.Context.Database.ExecuteSqlRaw($"UPDATE spawnpool SET pos_x = pos_x + 5 WHERE id = {LandingZoneFenceTurret.AfsPool}");

            migrator.Migrate(Migration);
            var pools = Pools(database);
            Assert.AreEqual(259.74, pools[LandingZoneFenceTurret.AfsPool].PosX, 0.001, "the GM's pool stays");
            CollectionAssert.Contains(Links(database), (4U, ControlPointLinkEntry.KindAfsPool, LandingZoneFenceTurret.AfsPool), "and its link");
            Assert.IsFalse(pools.ContainsKey(LandingZoneFenceTurret.BanePool), "the Bane pool goes all the same");

            migrator.Migrate(Previous);
            pools = Pools(database);
            Assert.AreEqual(259.74, pools[LandingZoneFenceTurret.AfsPool].PosX, 0.001, "Down leaves the GM's pool");
            Assert.IsTrue(pools.ContainsKey(LandingZoneFenceTurret.BanePool), "and puts the Bane pool back");
            Assert.AreEqual(1, Links(database).Count(l => l.ObjectId == LandingZoneFenceTurret.AfsPool), "one link, not two");
        }

        [TestMethod]
        public void OnMySqlThePairGoesAndComesBack()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in LandingZoneFenceTurret.Up())
                StringAssert.Contains(up, sql);
            StringAssert.Contains(up, $"'{Migration}'");
            foreach (var sql in LandingZoneFenceTurret.Down())
                StringAssert.Contains(down, sql);
        }

        private static object Row(SpawnPoolEntry p) =>
            (p.Mode, p.AnimType, p.RespawnTime, p.PosX, p.PosY, p.PosZ, p.Rotation, p.MapContextId, p.Creature1Id, p.Creature1MinCount,
                p.Creature1MaxCount, p.Creature2Id, p.Radius);

        private static Dictionary<uint, SpawnPoolEntry> Pools(OrdinaryLootTestDatabase database) =>
            database.Context.SpawnPoolEntries.AsNoTracking()
                .Where(p => p.Id >= ControlPointTurrets.PoolIdMin && p.Id <= ControlPointTurrets.PoolIdMax).ToDictionary(p => p.Id);

        private static List<(uint Point, byte Kind, uint ObjectId)> Links(OrdinaryLootTestDatabase database) =>
            database.Context.ControlPointLinkEntries.AsNoTracking().ToList()
                .Where(l => l.ObjectId >= ControlPointTurrets.PoolIdMin && l.ObjectId <= ControlPointTurrets.PoolIdMax)
                .Select(l => (l.ControlPointId, l.Kind, l.ObjectId)).ToList();
    }
}
