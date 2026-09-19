using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Models;
    using Packets;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;

    public class CellManager
    {
        private static CellManager _instance;
        private static readonly object InstanceLock = new object();
        public static readonly float CellSize = 25.6f;
        public static readonly float CellBias = 32768.0f;
        private readonly uint CellViewRange = 2;   // view 2 cell's in every direction
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        public static CellManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new CellManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private CellManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        //creature
        public void AddToWorld(MapChannel mapChannel, Creature creature)
        {
            if (creature == null)
                return;
            // register creature entity
            EntityManager.Instance.RegisterEntity(creature.EntityId, EntityType.Creature);
            EntityManager.Instance.RegisterCreature(creature);

            // calculate initial cell(x, z)
            var cellPosX = (uint)(creature.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(creature.Position.Z / CellSize + CellBias);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            // add creature to the cell
            mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].CreatureList.Add(creature);
            // add cellMatrix to creature
            creature.Cells = cellMatrix;

            // notify client's about new creatures
            var ListOfClients = new List<Client>();

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    ListOfClients.Add(client);

            CreatureManager.Instance.CellIntroduceCreatureToClients(mapChannel, creature, ListOfClients);
        }

        //mapTrigger
        public void AddToWorld(MapChannel mapChannel, MapTrigger trigger)
        {
            if (trigger == null)
                return;

            // calculate initial cell(x, z)
            var cellPosX = (uint)(trigger.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(trigger.Position.Z / CellSize + CellBias);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            // add mapTrigger to the cell
            mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].MapTriggers.Add(trigger);
        }

        //mapLink
        public void AddToWorld(MapChannel mapChannel, MapLink link)
        {
            if (link == null)
                return;

            var cellPosX = (uint)(link.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(link.Position.Z / CellSize + CellBias);

            // The matrix is built so the link's neighbours exist for the players who will look
            // at it from up to two cells away.
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].MapLinks.Add(link);
        }

        public void RemoveFromWorld(MapChannel mapChannel, MapLink link)
        {
            if (link == null)
                return;

            var cellSeed = GetCellSeed(link.Position);

            if (mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                cell.MapLinks.Remove(link);
        }

        // Object
        public void AddToWorld(MapChannel mapChannel, DynamicObject dynamicObject)
        {
            if (dynamicObject == null)
                return;

            // register object entity
            EntityManager.Instance.RegisterEntity(dynamicObject.EntityId, EntityType.Object);
            EntityManager.Instance.RegisterDynamicObject(dynamicObject);

            // calculate initial cell(x, z)
            var cellPosX = (uint)(dynamicObject.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(dynamicObject.Position.Z / CellSize + CellBias);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            // add Object to the cell
            mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].DynamicObjectList.Add(dynamicObject);

            // notify client's about new object
            var ListOfClients = new List<Client>();

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    ListOfClients.Add(client);

            DynamicObjectManager.Instance.CellIntroduceDynamicObjectToClients(dynamicObject, ListOfClients);
        }

        // Player
        public void AddToWorld(Client client)
        {
            if (client.Player == null)
                return;

            // calculate initial cell
            var CellPosX = (uint)(client.Player.Position.X / CellSize + CellBias);
            var CellPosZ = (uint)(client.Player.Position.Z / CellSize + CellBias);

            // create matrix
            var cellMatrix = CreateCellMatrix(client.Player.MapChannel, CellPosX, CellPosZ);

            // add client to the cell
            client.Player.MapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].ClientList.Add(client);
            // add cellMatrix to client
            client.Player.Cells = cellMatrix;

            // notify client about players, creatures, objects
            var ListOfClients = new List<Client>();
            var ListOfCreatures = new List<Creature>();
            var ListOfObjects = new List<DynamicObject>();

            foreach (var cellSeed in cellMatrix)
            {
                foreach (var player in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    ListOfClients.Add(player);

                foreach (var creature in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].CreatureList)
                    ListOfCreatures.Add(creature);

                foreach (var dinamicObject in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].DynamicObjectList)
                    ListOfObjects.Add(dinamicObject);

            }

            ManifestationManager.Instance.CellIntroduceClientToSefl(client);
            ManifestationManager.Instance.CellIntroduceClientToPlayers(client, ListOfClients);
            ManifestationManager.Instance.CellIntroducePlayersToClient(client, ListOfClients);

            CreatureManager.Instance.CellIntroduceCreaturesToClient(client, ListOfCreatures);
            DynamicObjectManager.Instance.CellIntroduceDynamicObjectsToClient(client, ListOfObjects);
        }

        internal void RemoveCreatureFromWorld(MapChannel mapChannel, Creature creature)
        {
            if (creature == null)
                return;

            // Tell the players who can see it. This used to go through DestroyPhysicalEntity
            // per player, which also unregisters the entity - so a corpse nobody was near when
            // it timed out was never unregistered at all, and stayed in the entity tables
            // (with its id never freed) for the life of the process.
            foreach (var cellSeed in creature.Cells)
                if (mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                    foreach (var player in cell.ClientList)
                        player.CallMethod(SysEntity.ClientMethodId, new DestroyPhysicalEntityPacket(creature.EntityId));

            // Its loot with it: a dispenser is a separate entity attached to the corpse, and
            // it was left in the map's table forever, with its id.
            LootDispenserManager.Instance.RemoveForCreature(mapChannel, creature);

            // Unregister once, whoever was or was not watching.
            EntityManager.Instance.UnregisterEntity(creature.EntityId);
            EntityManager.Instance.UnregisterCreature(creature.EntityId);
            EntityManager.Instance.FreeEntity(creature.EntityId);

            // remove creature from cell
            if (mapChannel.MapCellInfo.Cells.TryGetValue(creature.Cells[2, 2], out var homeCell))
                homeCell.CreatureList.Remove(creature);
        }

        public void DoWork(MapChannel mapChannel)
        {
            // 1 time per sec, do we need check more often?
            UpdateVisibility(mapChannel);
            // mob work

            // events etc...
        }

        public MapCell GetCell(MapChannel mapChannel, uint cellPosX, uint cellPosZ)
        {
            var cellSeed = (cellPosX & 0xFFFF) | (cellPosZ << 16);

            if (mapChannel.MapCellInfo.Cells.ContainsKey(cellSeed))
                return mapChannel.MapCellInfo.Cells[cellSeed];
            else
            {
                //create new cell
                var cell = new MapCell
                {
                    CellSeed = cellSeed,
                    CellPosX = cellPosX,
                    CellPosZ = cellPosZ
                };

                // register cell
                mapChannel.MapCellInfo.Cells.Add(cellSeed, cell);

                return cell;
            }
        }

        public void RemoveFromWorld(MapChannel mapChannel, DynamicObject dynObject)
        {
            if (dynObject == null)
                return;

            // unregister object entity
            EntityManager.Instance.UnregisterEntity(dynObject.EntityId);
            EntityManager.Instance.UnregisterDynamicObject(dynObject.EntityId);
            EntityManager.Instance.FreeEntity(dynObject.EntityId);

            var cellX = (uint)((dynObject.Position.X / CellSize) + CellBias);
            var cellZ = (uint)((dynObject.Position.Z / CellSize) + CellBias);
            var cellMatrix = CreateCellMatrix(mapChannel, cellX, cellZ);
            var ListOfClients = new List<Client>();

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    ListOfClients.Add(client);

            DynamicObjectManager.Instance.CellDiscardDynamicObjectToClients(dynObject.EntityId, ListOfClients);

            // remove object from cell
            mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].DynamicObjectList.Remove(dynObject);
        }

        public void RemoveFromWorld(MapChannel mapChannel, ulong entityId)
        {
            if (entityId == 0)
                return;

            DynamicObjectManager.Instance.CellDiscardDynamicObjectToClients(entityId, Server.Clients);
        }

        public uint GetCellSeed(Vector3 position)
        {
            var cellPosX = (uint)(position.X / CellSize + CellBias);
            var cellPosZ = (uint)(position.Z / CellSize + CellBias);
            var cellSeed = (cellPosX & 0xFFFF) | (cellPosZ << 16);

            return cellSeed;
        }

        public void RemoveFromWorld(Client client)
        {
            // Read after the null check, not before it.
            if (client.Player == null)
                return;

            var mapChannel = client.Player.MapChannel;

            if (mapChannel == null)
                return;

            //notify players
            var ListOfClients = new List<Client>();

            foreach (var cell in CellsIn(mapChannel, client.Player.Cells))
                ListOfClients.AddRange(cell.ClientList);

            ManifestationManager.Instance.CellDiscardClientToPlayers(client, ListOfClients);
            ManifestationManager.Instance.CellDiscardPlayersToClient(client, ListOfClients);

            // remove player from cell. A player who dropped during the loading screen never had
            // a matrix built, so this asked for cell 0 and threw - which abandoned the rest of
            // RemovePlayer on every such disconnect.
            if (mapChannel.MapCellInfo.Cells.TryGetValue(client.Player.Cells[2, 2], out var homeCell))
                homeCell.ClientList.Remove(client);
        }

        public void UpdateVisibility(MapChannel mapChannel)
        {
            foreach (var client in mapChannel.ClientList)
            {
                if (client.Player.Disconected || client.Player == null || client.State == ClientState.Loading)
                    continue;

                var cellPosX = (uint)(client.Player.Position.X / CellSize + CellBias);
                var cellPosZ = (uint)(client.Player.Position.Z / CellSize + CellBias);

                // create matrix
                var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

                // get info about cell we need to update
                var needUpdate = new List<uint>();
                var needDelete = new List<uint>();

                GetCellMatrixDiff(client.Player.Cells, cellMatrix, out needUpdate, out needDelete);

                // remove Player from old cell. A cell this map has not got is one the player was
                // never in, so there is nothing to take them out of.
                if (mapChannel.MapCellInfo.Cells.TryGetValue(client.Player.Cells[2, 2], out var oldCell))
                    oldCell.ClientList.Remove(client);

                // remove players, creatures, object that left visibility range
                var DiscardClients = new List<Client>();
                var DiscardCreatures = new List<Creature>();
                var DiscardObjects = new List<DynamicObject>();

                // The cells being left come from the player's stored matrix, which is the one
                // that can name cells of a map they are no longer on.
                foreach (var cellSeed in needDelete)
                {
                    if (!mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var leaving))
                        continue;

                    DiscardClients.AddRange(leaving.ClientList);
                    DiscardCreatures.AddRange(leaving.CreatureList);
                    DiscardObjects.AddRange(leaving.DynamicObjectList);
                }

                ManifestationManager.Instance.CellDiscardPlayersToClient(client, DiscardClients);
                CreatureManager.Instance.CellDiscardCreaturesToClient(client, DiscardCreatures);
                DynamicObjectManager.Instance.CellDiscardDynamicObjectsToClient(client, DiscardObjects);

                // add player to new cell
                mapChannel.MapCellInfo.Cells[cellMatrix[2, 2]].ClientList.Add(client);
                // set new player visibility
                client.Player.Cells = cellMatrix;

                // notify client about players, creatures, objects
                var AddClients = new List<Client>();
                var AddCreatures = new List<Creature>();
                var AddObjects = new List<DynamicObject>();

                foreach (var cellSeed in needUpdate)
                {
                    foreach (var player in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                        AddClients.Add(player);

                    foreach (var creature in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].CreatureList)
                        AddCreatures.Add(creature);

                    foreach (var dinamicObject in client.Player.MapChannel.MapCellInfo.Cells[cellSeed].DynamicObjectList)
                        AddObjects.Add(dinamicObject);
                }

                ManifestationManager.Instance.CellIntroduceClientToPlayers(client, AddClients);
                ManifestationManager.Instance.CellIntroducePlayersToClient(client, AddClients);
                CreatureManager.Instance.CellIntroduceCreaturesToClient(client, AddCreatures);
                DynamicObjectManager.Instance.CellIntroduceDynamicObjectsToClient(client, AddObjects);
            }
        }

        public void GetCellMatrixDiff(uint[,] oldCellMatrix, uint[,] newCellMatrix, out List<uint> needUpdate, out List<uint> needDelete)
        {
            var oldCells = new List<uint>();
            var newCells = new List<uint>();

            // find all cells that need to be removed
            foreach (var oldCell in oldCellMatrix)
            {
                var found = false;

                foreach (var newCell in newCellMatrix)
                    if (newCell == oldCell)
                    {
                        found = true;
                        break;
                    }

                if (!found)
                    oldCells.Add(oldCell);
            }

            // find all cells that need to be added
            foreach (var newCell in newCellMatrix)
            {
                var found = false;

                foreach (var oldCell in oldCellMatrix)
                    if (oldCell == newCell)
                    {
                        found = true;
                        break;
                    }

                if (!found)
                    newCells.Add(newCell);
            }

            needDelete = oldCells;
            needUpdate = newCells;
        }

        public uint[,] CreateCellMatrix(MapChannel mapChannel, uint cellPosX, uint cellPosZ)
        {
            var cellMatrix = new uint[5, 5];
            cellMatrix[0, 0] = GetCell(mapChannel, cellPosX - 2, cellPosZ - 2).CellSeed;
            cellMatrix[0, 1] = GetCell(mapChannel, cellPosX - 2, cellPosZ - 1).CellSeed;
            cellMatrix[0, 2] = GetCell(mapChannel, cellPosX - 2, cellPosZ).CellSeed;
            cellMatrix[0, 3] = GetCell(mapChannel, cellPosX - 2, cellPosZ + 1).CellSeed;
            cellMatrix[0, 4] = GetCell(mapChannel, cellPosX - 2, cellPosZ + 2).CellSeed;
            cellMatrix[1, 0] = GetCell(mapChannel, cellPosX - 1, cellPosZ - 2).CellSeed;
            cellMatrix[1, 1] = GetCell(mapChannel, cellPosX - 1, cellPosZ - 1).CellSeed;
            cellMatrix[1, 2] = GetCell(mapChannel, cellPosX - 1, cellPosZ).CellSeed;
            cellMatrix[1, 3] = GetCell(mapChannel, cellPosX - 1, cellPosZ + 1).CellSeed;
            cellMatrix[1, 4] = GetCell(mapChannel, cellPosX - 1, cellPosZ + 2).CellSeed;
            cellMatrix[2, 0] = GetCell(mapChannel, cellPosX, cellPosZ - 2).CellSeed;
            cellMatrix[2, 1] = GetCell(mapChannel, cellPosX, cellPosZ - 1).CellSeed;
            cellMatrix[2, 2] = GetCell(mapChannel, cellPosX, cellPosZ).CellSeed;        // Actor is here
            cellMatrix[2, 3] = GetCell(mapChannel, cellPosX, cellPosZ + 1).CellSeed;
            cellMatrix[2, 4] = GetCell(mapChannel, cellPosX, cellPosZ + 2).CellSeed;
            cellMatrix[3, 0] = GetCell(mapChannel, cellPosX + 1, cellPosZ - 2).CellSeed;
            cellMatrix[3, 1] = GetCell(mapChannel, cellPosX + 1, cellPosZ - 1).CellSeed;
            cellMatrix[3, 2] = GetCell(mapChannel, cellPosX + 1, cellPosZ).CellSeed;
            cellMatrix[3, 3] = GetCell(mapChannel, cellPosX + 1, cellPosZ + 1).CellSeed;
            cellMatrix[3, 4] = GetCell(mapChannel, cellPosX + 1, cellPosZ + 2).CellSeed;
            cellMatrix[4, 0] = GetCell(mapChannel, cellPosX + 2, cellPosZ - 2).CellSeed;
            cellMatrix[4, 1] = GetCell(mapChannel, cellPosX + 2, cellPosZ - 1).CellSeed;
            cellMatrix[4, 2] = GetCell(mapChannel, cellPosX + 2, cellPosZ).CellSeed;
            cellMatrix[4, 3] = GetCell(mapChannel, cellPosX + 2, cellPosZ + 1).CellSeed;
            cellMatrix[4, 4] = GetCell(mapChannel, cellPosX + 2, cellPosZ + 2).CellSeed;

            return cellMatrix;
        }

        #region SendPackets
        internal void CellMoveObject(Creature creature, Movement movementData)
        {
            // calculate initial cell(x, z)
            var cellPosX = (uint)(creature.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(creature.Position.Z / CellSize + CellBias);
            var mapChannel = MapChannelManager.Instance.FindByContextId(creature.MapContextId);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    client.MoveObject(creature.EntityId, movementData);
        }

        internal void CellCallMethod(DynamicObject obj, PythonPacket packet)
        {
            // calculate initial cell(x, z)
            var cellPosX = (uint)(obj.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(obj.Position.Z / CellSize + CellBias);
            var mapChannel = MapChannelManager.Instance.FindByContextId(obj.MapContextId);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    client.CallMethod(obj.EntityId, packet);
        }

        internal void CellCallMethod(Creature creature, PythonPacket packet)
        {
            // calculate initial cell(x, z)
            var cellPosX = (uint)(creature.Position.X / CellSize + CellBias);
            var cellPosZ = (uint)(creature.Position.Z / CellSize + CellBias);
            var mapChannel = MapChannelManager.Instance.FindByContextId(creature.MapContextId);

            // create matrix
            var cellMatrix = CreateCellMatrix(mapChannel, cellPosX, cellPosZ);

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    client.CallMethod(creature.EntityId, packet);
        }

        internal void CellCallMethod(MapChannel mapChannel, Actor origin, PythonPacket packet)
        {
            foreach (var cell in CellsIn(mapChannel, origin.Cells))
                foreach (var client in cell.ClientList)
                    client.CallMethod(origin.EntityId, packet);
        }

        /// <summary>
        /// The cells of <paramref name="mapChannel"/> that a stored cell matrix names, skipping
        /// any this map does not have.
        ///
        /// A matrix belongs to the map it was built for, but an actor's outlives that: nothing
        /// clears it when they leave a map, and it is five by five zeroes until they first enter
        /// one. Indexing a map's cell table with one straight - which is what every broadcast
        /// over a stored matrix used to do - throws KeyNotFoundException whenever the two do not
        /// belong together. On the world loop that costs the rest of the tick, and for something
        /// re-run every tick it costs every tick after it as well.
        ///
        /// A cell the matrix names that this map has not got is simply nobody to send to, so it
        /// is skipped rather than being an error.
        /// </summary>
        internal static IEnumerable<MapCell> CellsIn(MapChannel mapChannel, uint[,] cellMatrix)
        {
            if (mapChannel == null || cellMatrix == null)
                yield break;

            foreach (var cellSeed in cellMatrix)
                if (mapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                    yield return cell;
        }
        #endregion
    }
}
