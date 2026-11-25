using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Navigation;
    using Rasa.Services.Preloader;
    using Rasa.Structures.World;
    using Rasa.Test.Database;

    using D = Rasa.Services.Preloader.EdmundRangeDefenses;

    /// <summary>
    /// Edmund Range's control points' turrets and force fields (Edmund_range_defenses,
    /// EdmundRangeDefenses): six bases at Whiskey and Echo and four at Charlie, each on the round
    /// floor its fence piece builds and facing out from its point; four fields a point, one in
    /// each side's gateway; the rows and links in, Up again changing nothing, and Down taking
    /// them out.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EdmundRangeDefensesTests
    {
        private const string Migration = "20271205000000_Edmund_range_defenses", Previous = "20271204000000_Lightbender_gland_snipers";

        private static readonly Dictionary<uint, Vector3> Points = new()
        {
            [EdmundRangeSeed.WhiskeyId] = new Vector3(-287.959f, 423.258f, -95.099f),
            [EdmundRangeSeed.CharlieId] = new Vector3(-60f, 431.754f, 40f),
            [EdmundRangeSeed.EchoId] = new Vector3(170f, 423.163f, 175f)
        };

        [TestMethod]
        public void TheTurretsStandOnTheRoundBasesAndFaceOutFromTheirPoints()
        {
            CollectionAssert.AreEqual(new[] { 6, 4, 6 },
                Points.Keys.OrderBy(id => id).Select(id => D.Spots.Count(s => s.Point == id)).ToArray(), "Whiskey, Charlie, Echo");

            var nav = new NavMeshQuery(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), "adv_wargame_edmundrange2")));

            foreach (var spot in D.Spots)
            {
                var at = new Vector3((float)spot.X, (float)spot.Y, (float)spot.Z);
                var point = Points[spot.Point];

                // The base's floor is under the gun, 6.24 m up the fence piece and 4 to 5 m over the
                // ground the piece is sunk into.
                var floor = nav.NearestInColumn(at + new Vector3(0, 1, 0), 2f);
                Assert.IsNotNull(floor, spot.Where);
                Assert.IsLessThan(0.3f, Math.Abs(floor.Value.Y - at.Y), $"{spot.Where}: floor {floor.Value.Y}");
                var ground = nav.NearestInColumn(at - new Vector3(0, 6, 0), 1.5f);
                Assert.IsNotNull(ground, $"{spot.Where}: no ground under it");
                Assert.IsGreaterThan(4f, at.Y - ground.Value.Y, $"{spot.Where}: over the ground");

                // Out from the point: the pool's rotation is the bearing from the point, turned half round.
                var bearing = Math.Atan2(spot.X - point.X, spot.Z - point.Z) + Math.PI;
                var off = Math.IEEERemainder(bearing - spot.Rotation, 2 * Math.PI);
                Assert.IsLessThan(0.001, Math.Abs(off), spot.Where);

                var reach = new Vector2((float)spot.X - point.X, (float)spot.Z - point.Z).Length();
                Assert.IsTrue(reach is > 35f and < 70f, $"{spot.Where}: {reach} m from its point");
            }
        }

        [TestMethod]
        public void EachPointHasAFieldInTheGatewayOfEachSide()
        {
            var nav = new NavMeshQuery(NavMeshFile.Read(NavMeshFile.PathFor(NavMeshDirectory(), "adv_wargame_edmundrange2")));

            foreach (var group in D.Gates.GroupBy(g => g.Point))
            {
                Assert.AreEqual(4, group.Count());
                var point = Points[group.Key];

                // One a side: their bearings from the point a quarter turn apart.
                var bearings = group.Select(g => Math.Atan2(g.X - point.X, g.Z - point.Z)).OrderBy(b => b).ToList();
                for (var i = 1; i < bearings.Count; i++)
                    Assert.AreEqual(Math.PI / 2, bearings[i] - bearings[i - 1], 0.05, $"point {group.Key}");

                foreach (var gate in group)
                {
                    // Across the way in: the sheet faces the point, and its foot, 1.12 m up from the gate's
                    // origin, which is sunk into the ground, is at the gateway's floor.
                    var facing = new Vector2((float)Math.Sin(gate.Rotation), (float)Math.Cos(gate.Rotation));
                    var toPoint = Vector2.Normalize(new Vector2(point.X - (float)gate.X, point.Z - (float)gate.Z));
                    Assert.AreEqual(1, Math.Abs(Vector2.Dot(facing, toPoint)), 0.001, gate.Where);

                    var ground = nav.GroundHeight(new Vector3((float)gate.X, (float)gate.Y + 2, (float)gate.Z));
                    Assert.IsNotNull(ground, gate.Where);
                    Assert.IsLessThan(0.6f, Math.Abs(ground.Value - ((float)gate.Y + 1.12f)), $"{gate.Where}: floor {ground}");
                }
            }
        }

        [TestMethod]
        public void UpPutsTheRowsAndLinksInAndDownTakesThemOut()
        {
            using var database = new OrdinaryLootTestDatabase();
            var migrator = database.Context.GetService<IMigrator>();
            long Count(string sql) => database.Context.Database.SqlQueryRaw<long>(sql).ToArray().Single();

            const string Links = "SELECT COUNT(*) AS Value FROM control_point_link WHERE control_point_id BETWEEN 101 AND 103";
            const string Pools = "SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 595201 AND 595236";
            const string Fields = "SELECT COUNT(*) AS Value FROM world_destructible WHERE id BETWEEN 595301 AND 595312";

            migrator.Migrate(Previous);
            var links = Count(Links);
            Assert.AreEqual(0L, Count(Pools));
            Assert.AreEqual(0L, Count(Fields));

            migrator.Migrate(Migration);
            Assert.AreEqual(32L, Count(Pools));
            Assert.AreEqual(12L, Count(Fields));
            Assert.AreEqual(links + 44, Count(Links));
            Assert.AreEqual(16L, Count("SELECT COUNT(*) AS Value FROM spawnpool WHERE id BETWEEN 595201 AND 595216 AND creature_1_Id = 550019 AND mode = 1 AND respown_time = 1800"));
            Assert.AreEqual(16L, Count("SELECT COUNT(*) AS Value FROM control_point_link WHERE kind = 2 AND object_id BETWEEN 595201 AND 595216"));
            Assert.AreEqual(16L, Count("SELECT COUNT(*) AS Value FROM control_point_link WHERE kind = 1 AND object_id BETWEEN 595221 AND 595236"));
            Assert.AreEqual(12L, Count("SELECT COUNT(*) AS Value FROM control_point_link WHERE kind = 8 AND object_id BETWEEN 595301 AND 595312"));
            Assert.AreEqual(12L, Count("SELECT COUNT(*) AS Value FROM world_destructible WHERE id BETWEEN 595301 AND 595312 AND class_id = 10000083 AND map_context_id = 2374"));

            // Each Bane turret 0.38 m over its base, where the holder's stands.
            for (var i = 0; i < D.Spots.Length; i++)
                Assert.AreEqual(1L, Count($"SELECT COUNT(*) AS Value FROM spawnpool a JOIN spawnpool b ON b.id = {D.BanePoolOf(i)} " +
                    $"WHERE a.id = {D.TeamPoolOf(i)} AND b.creature_1_Id = 551014 AND ABS(b.pos_y - a.pos_y - 0.38) < 0.001 AND a.pos_x = b.pos_x AND a.pos_z = b.pos_z"));

            foreach (var sql in D.Up())
                database.Context.Database.ExecuteSqlRaw(sql);
            Assert.AreEqual(32L, Count(Pools), "Up again");
            Assert.AreEqual(12L, Count(Fields));
            Assert.AreEqual(links + 44, Count(Links));

            migrator.Migrate(Previous);
            Assert.AreEqual(0L, Count(Pools));
            Assert.AreEqual(0L, Count(Fields));
            Assert.AreEqual(links, Count(Links), "the seed's stay");
        }

        [TestMethod]
        public void OnMySqlTheRowsGoInAndComeOut()
        {
            using var context = PersistenceIntegrationTests.CreateContext(typeof(Rasa.Context.World.MySqlWorldContext), "unused");
            var migrator = context.GetService<IMigrator>();
            var up = migrator.GenerateScript(Previous, Migration);
            var down = migrator.GenerateScript(Migration, Previous);

            foreach (var sql in D.Up())
                StringAssert.Contains(up, sql);
            foreach (var sql in D.Down())
                StringAssert.Contains(down, sql);
        }

        private static string NavMeshDirectory()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var navmesh = Path.Combine(dir.FullName, "navmesh");
                if (Directory.Exists(navmesh))
                    return navmesh;
            }

            throw new DirectoryNotFoundException("navmesh");
        }
    }
}
