using System;
using System.IO;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.World
{
    using Rasa.Managers;
    using Rasa.Navigation;
    using Rasa.Structures;
    using Rasa.Test.Gameplay;

    /// <summary>
    /// Two creatures found standing in each other are pushed apart across the ground, not up and down
    /// (BehaviorManager.KeepApart), and a talkable NPC is not pushed off his post. A wanderer passing Ranger Benos in
    /// Alia Das pushed him a quarter of their difference in height lower at every pass, and he sank into the ground.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class KeepApartTests
    {
        /// <summary>Ranger Benos's post in Alia Das, the ground under it, and the slope a wanderer comes down above him.</summary>
        private static readonly Vector3 Post = new(773.8867f, 301.5887f, 493.9375f);

        [TestMethod]
        public void APassingWandererNeverMovesAPostedNpc()
        {
            using var world = new WorldTestContext();
            var benos = ShotFlightTests.Spawn(world, Post);
            benos.Npc = new Npc();
            var wanderer = ShotFlightTests.Spawn(world, Post + new Vector3(-0.4f, 0.6f, -0.3f));

            for (var pass = 0; pass < 200; pass++)
            {
                wanderer.Position = Post + new Vector3(-0.4f, 0.6f, -0.3f);
                BehaviorManager.KeepApart(world.Map, benos, wanderer);
                BehaviorManager.KeepApart(world.Map, wanderer, benos);
            }

            Assert.AreEqual(Post, benos.Position, "where he was put, at his height");
        }

        [TestMethod]
        public void ThePushIsAcrossTheGroundAndKeepsBothOnIt()
        {
            using var world = new WorldTestContext();
            world.Map.NavMesh = Wilderness();
            var low = ShotFlightTests.Spawn(world, Post);
            var high = ShotFlightTests.Spawn(world, Ground(world, Post + new Vector3(-0.5f, 0, -0.4f)));
            high.Position += new Vector3(0, 0.6f, 0);       // over the ground: the height the old push took the quarter of
            var lowAcross = new Vector2(low.Position.X, low.Position.Z);

            BehaviorManager.KeepApart(world.Map, low, high);

            Assert.AreEqual(BehaviorManager.KeepApartPush, Vector2.Distance(lowAcross, new Vector2(low.Position.X, low.Position.Z)), 1e-3f, "pushed across");
            Assert.AreEqual(Ground(world, low.Position).Y, low.Position.Y, 1e-3f, "and on the ground, not a quarter of the difference lower");
            Assert.AreEqual(Ground(world, high.Position).Y, high.Position.Y, 1e-3f);
        }

        [TestMethod]
        public void TheOtherTakesBothSharesAndTwoPostedNpcsStay()
        {
            using var world = new WorldTestContext();
            var posted = ShotFlightTests.Spawn(world, new Vector3(10, 0, 10));
            posted.Npc = new Npc();
            var other = ShotFlightTests.Spawn(world, new Vector3(10.5f, 0, 10));

            BehaviorManager.KeepApart(world.Map, posted, other);
            Assert.AreEqual(new Vector3(10, 0, 10), posted.Position);
            Assert.AreEqual(new Vector3(10.5f + 2 * BehaviorManager.KeepApartPush, 0, 10), other.Position);

            var second = ShotFlightTests.Spawn(world, new Vector3(10.5f, 0, 10));
            second.Npc = new Npc();
            BehaviorManager.KeepApart(world.Map, posted, second);
            Assert.AreEqual(new Vector3(10, 0, 10), posted.Position);
            Assert.AreEqual(new Vector3(10.5f, 0, 10), second.Position);
        }

        [TestMethod]
        public void TwoWanderersArePushedAShareEachAndKeepTheirHeights()
        {
            using var world = new WorldTestContext();
            var one = ShotFlightTests.Spawn(world, new Vector3(10, 1, 10));
            var two = ShotFlightTests.Spawn(world, new Vector3(10.5f, 2, 10));

            BehaviorManager.KeepApart(world.Map, one, two);

            Assert.AreEqual(new Vector3(10 - BehaviorManager.KeepApartPush, 1, 10), one.Position);
            Assert.AreEqual(new Vector3(10.5f + BehaviorManager.KeepApartPush, 2, 10), two.Position);
        }

        private static Vector3 Ground(WorldTestContext world, Vector3 at) => world.Map.NavMesh.Nearest(at).Value;

        private static NavMeshQuery Wilderness()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Rasa.NET.sln")))
                directory = directory.Parent;
            return TestNavMeshes.Query(NavMeshFile.PathFor(Path.Combine(directory!.FullName, "navmesh"), "adv_foreas_concordia_wilderness"));
        }
    }
}
