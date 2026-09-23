using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Repositories.UnitOfWork;
    using Structures;

    public class SpawnPoolManager
    {
        private static SpawnPoolManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public readonly Dictionary<uint, SpawnPool> LoadedSpawnPools = new Dictionary<uint, SpawnPool>();

        /// <summary>spawnpool.mode: the pool spawns on its own timer. Every pool before mode was used.</summary>
        public const short ModeAutomatic = 0;

        /// <summary>
        /// spawnpool.mode: the pool is a control point's garrison. At a control point the Bane
        /// camp is the control point itself, and the hospital, token banker and vendors the
        /// client labels "(Control Point)" are what stands there once AFS has taken it; the two
        /// never stand together. The server has no control point ownership yet and the AFS side
        /// is what it seeds, so these pools are dormant: the garrison of a point AFS holds.
        /// </summary>
        public const short ModeControlPoint = 1;

        /// <summary>
        /// spawnpool.mode: spawned only when something asks for it - a script, a GM. Also where a
        /// mined pool goes that stood on a place players revive at and could not be moved blind.
        /// </summary>
        public const short ModeScripted = 2;

        /// <summary>How close a hostile pool's area may come to a friendly NPC, a hospital or a waypoint.</summary>
        public const float SafeClearance = 15f;

        public static SpawnPoolManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new SpawnPoolManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private SpawnPoolManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        public void IncreaseQueueCount(SpawnPool spawnPool)
        {
            spawnPool.DropshipQueue++;
        }

        public void DecreaseQueueCount(SpawnPool spawnPool)
        {
            spawnPool.DropshipQueue--;

            if ((spawnPool.DropshipQueue + spawnPool.QueuedCreatures + spawnPool.AliveCreatures) == 0)
                spawnPool.UpdateTimer = 0;
        }

        public void IncreaseQueuedCreatureCount(SpawnPool spawnPool, int count)
        {
            spawnPool.QueuedCreatures += count;
        }

        internal void DecreaseQueuedCreatureCount(SpawnPool spawnPool, int count)
        {
            spawnPool.QueuedCreatures -= count;

            if ((spawnPool.DropshipQueue + spawnPool.QueuedCreatures + spawnPool.AliveCreatures) == 0)
                spawnPool.UpdateTimer = 0;
        }

        public void IncreaseAliveCreatureCount(SpawnPool spawnPool)
        {
            spawnPool.AliveCreatures++;
        }

        internal void DecreaseAliveCreatureCount(MapChannel mapChannel, SpawnPool spawnPool)
        {
            spawnPool.AliveCreatures--;
            if ((spawnPool.DropshipQueue + spawnPool.QueuedCreatures + spawnPool.AliveCreatures) == 0)
                spawnPool.UpdateTimer = 0;
        }

        public void IncreaseDeadCreatureCount(SpawnPool spawnPool)
        {
            spawnPool.DeadCreatures++;
        }

        internal void DecreaseDeadCreatureCount(SpawnPool spawnPool)
        {
            spawnPool.DeadCreatures--;
        }

        public void SpawnPoolInit()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var spawnPoolList = unitOfWork.Spawnpools.Get();

            foreach (var data in spawnPoolList)
            {
                var spawnPoolSlots = new List<SpawnPoolSlot>();

                if (data.Creature1Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature1Id, data.Creature1MinCount, data.Creature1MaxCount));
                if (data.Creature2Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature2Id, data.Creature2MinCount, data.Creature2MaxCount));
                if (data.Creature3Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature3Id, data.Creature3MinCount, data.Creature3MaxCount));
                if (data.Creature4Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature4Id, data.Creature4MinCount, data.Creature4MaxCount));
                if (data.Creature5Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature5Id, data.Creature5MinCount, data.Creature5MaxCount));
                if (data.Creature6Id > 0)
                    spawnPoolSlots.Add(new SpawnPoolSlot(data.Creature6Id, data.Creature6MinCount, data.Creature6MaxCount));

                var spawnPool = new SpawnPool
                {
                    AnimType = data.AnimType,
                    MapContextId = data.MapContextId,
                    DbId = data.Id,
                    Position = data.Position,
                    Rotation = (float)data.Rotation,
                    Radius = (float)data.Radius,
                    Mode = data.Mode,
                    RespawnTime = data.RespawnTime * 100,  //convert to ms
                    // to spawn all cretures at server start, we set UpdateTimer to RespawnTime
                    UpdateTimer = data.RespawnTime * 100, //convert to ms
                    SpawnSlot = spawnPoolSlots
                };

                LoadedSpawnPools.Add(data.Id, spawnPool);
            }

            Logger.WriteLog(LogType.Initialize, $"Loaded {LoadedSpawnPools.Count} SpawnPools");
        }

        public void SpawnPoolWorker(MapChannel mapChannel, long timePassed)
        {
            foreach (var key in LoadedSpawnPools)
            {
                var spawnPool = key.Value;

                if (spawnPool.MapContextId != mapChannel.MapInfo.MapContextId)
                    continue; // spawnpool is not for this map

                if (spawnPool.Mode != ModeAutomatic)
                    continue; // a control point's garrison or a scripted pool: not on a timer

                var totalCreaturesActive = spawnPool.AliveCreatures + spawnPool.QueuedCreatures;

                if (totalCreaturesActive > 0)
                    continue; // there is still active creatures

                spawnPool.UpdateTimer += timePassed;

                if (spawnPool.UpdateTimer < spawnPool.RespawnTime)
                    continue; // spawnpool is still on cooldown

                // create list of creatures to spawn
                var creatureList = CreateListOfCreatures(spawnPool);

                if (creatureList.Count == 0)
                    continue; // nothing to spawn

                if (spawnPool.AnimType == 0)    // animType==0; spawn without animation
                {
                    IncreaseQueuedCreatureCount(spawnPool, creatureList.Count);

                    SpawnCreatures(spawnPool, creatureList);

                    DecreaseQueuedCreatureCount(spawnPool, creatureList.Count);
                }
                // animType == 1; bane dropship animation
                else if (spawnPool.AnimType == 1)
                {
                    IncreaseQueueCount(spawnPool);
                    IncreaseQueuedCreatureCount(spawnPool, creatureList.Count);

                    // create bane_dropship
                    var dropship = new Dropship(TargetCategory.Hostile, DropshipType.Spawner, spawnPool);

                    CellManager.Instance.AddToWorld(mapChannel, dropship);

                    DynamicObjectManager.Instance.Dropships.Add(dropship.EntityId, dropship);
                }
                // animType == 2; human dropship animation
                else if (spawnPool.AnimType == 2)
                {
                    IncreaseQueueCount(spawnPool);
                    IncreaseQueuedCreatureCount(spawnPool, creatureList.Count);

                    // create human_dropship
                    var dropship = new Dropship(TargetCategory.Friendly, DropshipType.Spawner, spawnPool);

                    CellManager.Instance.AddToWorld(mapChannel, dropship);

                    DynamicObjectManager.Instance.Dropships.Add(dropship.EntityId, dropship);

                }
            }
        }

        internal void SpawnCreatures(SpawnPool spawnPool,List<Creature> creatureList)
        {
            var mapChannel = MapChannelManager.Instance.FindByContextId(spawnPool.MapContextId);

            foreach (var spawnSlot in creatureList)
            {
                var creature = CreatureManager.Instance.CreateCreature(spawnSlot.DbId, spawnPool);

                if (creature == null)
                    continue;

                RandomizePosition(creature, creatureList.Count);

                CellManager.Instance.AddToWorld(mapChannel, creature);
            }
        }

        internal List<Creature> CreateListOfCreatures(SpawnPool spawnPool)
        {
            var creatureList = new List<Creature>();

            foreach (var spawnSlot in spawnPool.SpawnSlot)
            {
                var spawnCreatureCount = new Random().Next(spawnSlot.CountMin, spawnSlot.CountMax + 1);

                for (var i = 0; i < spawnCreatureCount; i++)
                {
                    creatureList.Add(new Creature(CreatureManager.Instance.LoadedCreatures[spawnSlot.CreatureId]));

                    if (creatureList.Count > 63)    // cannot spawn more than 64 creatures at once
                        break;
                }
            }

            return creatureList;
        }

        internal void RandomizePosition(Creature creature, int count)
        {
            var pool = creature.SpawnPool;
            var mapChannel = MapChannelManager.Instance.FindByContextId(pool.MapContextId);

            CreatureManager.Instance.SetLocation(creature, SpawnPoint(mapChannel, pool, count), pool.Rotation, pool.MapContextId);
        }

        /// <summary>
        /// Where one of the pool's creatures stands. A pool with a radius is an area - a camp,
        /// a nest - and its creatures are spread across it: a walkable point anywhere inside the
        /// radius when the map has a navmesh, otherwise a point in the disc snapped to whatever
        /// ground there is. A pool without one is the old point: two units of scatter when more
        /// than one creature shares it, then snapped to the ground so members on a slope neither
        /// hang in the air nor start in it.
        ///
        /// A mined area's centre is the middle of the props it was built from, and its height
        /// their average: it can be inside a pillbox, or a few metres above or below the floor,
        /// where the navmesh's point query (4 m across, 8 m up and down) finds nothing. Then the
        /// nearest walkable point to the centre, within the pool's own radius, stands in for it -
        /// or, for a centre whose height is a map label's guess, the nearest walkable point in
        /// the column above and below it.
        /// </summary>
        internal static Vector3 SpawnPoint(MapChannel mapChannel, SpawnPool pool, int count)
        {
            var pos = pool.Position;

            if (pool.Radius > 0)
            {
                var walkable = NavMeshManager.RandomPointAround(mapChannel, pos, pool.Radius);

                if (!walkable.HasValue
                    && (NavMeshManager.NearestWalkable(mapChannel, pos, Math.Max(32f, pool.Radius))
                        ?? NavMeshManager.NearestInColumn(mapChannel, pos)) is Vector3 anchor)
                    walkable = NavMeshManager.RandomPointAround(mapChannel, anchor, pool.Radius) ?? anchor;

                if (walkable.HasValue)
                    return walkable.Value;

                pos += InDisc(pool.Radius);
            }
            else if (count != 1)
            {
                pos.X += new Random().Next() % 5 - 2;
                pos.Z += new Random().Next() % 5 - 2;
            }

            return NavMeshManager.SnapToGround(mapChannel, pos);
        }

        /// <summary>
        /// Every automatic pool of hostile creatures whose area comes within SafeClearance of a
        /// friendly NPC's pool, a hospital or a waypoint pad: a player reviving or arriving there
        /// would stand in a fight. Logged and recorded for the map; nothing is changed. Run once
        /// the creatures, the pools and the teleporters are all loaded.
        /// </summary>
        public void ValidatePools()
        {
            var safe = new Dictionary<uint, List<(Vector3 Position, string What)>>();

            void Add(uint map, Vector3 position, string what)
            {
                if (!safe.TryGetValue(map, out var list))
                    safe[map] = list = new List<(Vector3, string)>();

                list.Add((position, what));
            }

            foreach (var pool in LoadedSpawnPools.Values)
                if (pool.SpawnSlot.Exists(s => Side(s.CreatureId) == TargetCategory.Friendly))
                    Add(pool.MapContextId, pool.Position, $"the NPCs of pool {pool.DbId}");

            foreach (var teleporter in DynamicObjectManager.Instance.Teleporters.Values)
                if (teleporter.ObjectData is WaypointInfo info && (info.WaypointType == WaypointType.Hospital || info.WaypointType == WaypointType.Waypoint))
                    Add(teleporter.MapContextId, teleporter.Position, $"{info.WaypointType} {info.WaypointId}");

            var bad = 0;

            foreach (var pool in LoadedSpawnPools.Values)
            {
                // Not on a timer, never brings anything (min and max 0), or not all hostile.
                if (pool.Mode != ModeAutomatic || !pool.SpawnSlot.Exists(s => s.CountMax > 0)
                    || !pool.SpawnSlot.TrueForAll(s => Side(s.CreatureId) == TargetCategory.Hostile))
                    continue;

                if (!safe.TryGetValue(pool.MapContextId, out var points))
                    continue;

                foreach (var (position, what) in points)
                {
                    var gap = Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(pool.Position.X, pool.Position.Z)) - pool.Radius;

                    if (gap >= SafeClearance)
                        continue;

                    bad++;
                    var message = $"spawnpool {pool.DbId}: its creatures can stand {Math.Max(0, gap):0} m from {what}, which should be safe ground.";
                    Logger.WriteLog(LogType.Error, message);
                    MapErrorManager.Instance.Record(pool.MapContextId, message);
                    break;
                }
            }

            Logger.WriteLog(LogType.Initialize, $"SpawnPools checked against safe ground: {bad} too close.");
        }

        private static TargetCategory Side(uint creatureId) =>
            CreatureManager.Instance.LoadedCreatures.TryGetValue(creatureId, out var creature) ? creature.TargetCategory : TargetCategory.Hostile;

        /// <summary>A point uniformly inside a disc of this radius, on the ground plane.</summary>
        internal static Vector3 InDisc(float radius)
        {
            var random = new Random();
            var angle = random.NextDouble() * Math.PI * 2;
            var distance = radius * Math.Sqrt(random.NextDouble());

            return new Vector3((float)(Math.Cos(angle) * distance), 0, (float)(Math.Sin(angle) * distance));
        }
    }
}
