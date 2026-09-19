using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;

    /// <summary>
    /// Tells each client which of its map's regions the player is standing in. A region is the
    /// client's per-area ambient sound, music, sky, environment map, display name and, for
    /// caverns, its own minimap; the .map carries the table of them and the client applies the
    /// highest-priority ones it is told about (regionmanager.py, radarwindow.py). Which regions
    /// a player was in was decided by server-side volumes that did not survive, so the volumes
    /// here are authored rows in map_region: seeded from the client's region labels and cavern
    /// minimap rectangles, adjusted with the .region commands.
    ///
    /// Once a second per map the set of enabled volumes containing each player is compared with
    /// what they were last sent, and UpdateRegions goes out on a change. Volumes can be limited
    /// to players who are underground - on navmesh flagged as lying under the terrain - or on the
    /// surface, which is what keeps a cavern's rectangle from applying to the hillside above it.
    /// </summary>
    public class RegionManager
    {
        private static RegionManager _instance;
        private static readonly object InstanceLock = new object();

        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        private readonly Dictionary<uint, MapRegion> _regions = new Dictionary<uint, MapRegion>();
        private readonly Dictionary<uint, List<MapRegion>> _byMap = new Dictionary<uint, List<MapRegion>>();

        public static RegionManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new RegionManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private RegionManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        public bool TryGet(uint id, out MapRegion region)
        {
            return _regions.TryGetValue(id, out region);
        }

        /// <summary>Loads map_region. Runs after MapChannelInit.</summary>
        public void RegionInit()
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var entries = unitOfWork.MapRegions.GetMapRegions();
            var unloadedMaps = new HashSet<uint>();

            foreach (var entry in entries)
            {
                var region = MapRegion.FromEntry(entry);

                if (!MapChannelManager.Instance.MapChannelArray.ContainsKey(region.MapContextId))
                    unloadedMaps.Add(region.MapContextId);

                Register(region);
            }

            Logger.WriteLog(LogType.Initialize, $"Loaded {entries.Count} region volumes on {_byMap.Count} maps");

            foreach (var mapContextId in unloadedMaps)
                Logger.WriteLog(LogType.Initialize, $"  map {mapContextId} is not loaded; its region volumes will not apply");
        }

        private void Register(MapRegion region)
        {
            _regions[region.Id] = region;

            if (!_byMap.TryGetValue(region.MapContextId, out var list))
                _byMap[region.MapContextId] = list = new List<MapRegion>();

            list.Add(region);
        }

        private void Unregister(MapRegion region)
        {
            _regions.Remove(region.Id);

            if (_byMap.TryGetValue(region.MapContextId, out var list))
                list.Remove(region);
        }

        /// <summary>Once a second per map, from MapChannelWorker.</summary>
        public void Worker(MapChannel mapChannel)
        {
            if (mapChannel.ClientList.Count == 0)
                return;

            foreach (var client in mapChannel.ClientList)
            {
                var player = client?.Player;

                if (player == null || player.Disconected || client.State != ClientState.Ingame || player.MapChannel != mapChannel)
                    continue;

                Refresh(client, false);
            }
        }

        /// <summary>
        /// Called when a player has been placed on a map (MapLoaded): the first UpdateRegions
        /// for this map, whatever they were last told on the previous one.
        /// </summary>
        public void PlayerEnteredMap(Client client)
        {
            if (client.Player?.MapChannel == null)
                return;

            client.Player.RegionIds = null;
            client.Player.RegionsHeld = false;
            Refresh(client, true);
        }

        public void RemovePlayer(Client client)
        {
            if (client.Player == null)
                return;

            client.Player.RegionIds = null;
            client.Player.RegionsHeld = false;
        }

        /// <summary>.setregion: sends exactly this list and keeps the worker from replacing it.</summary>
        public void Hold(Client client, List<uint> regionIds)
        {
            var player = client.Player;
            regionIds.Sort();
            player.RegionIds = regionIds;
            player.RegionsHeld = true;
            client.CallMethod(player.EntityId, new UpdateRegionsPacket(regionIds));
        }

        /// <summary>.setregion off: back to what the volumes say.</summary>
        public void Release(Client client)
        {
            client.Player.RegionsHeld = false;
            Refresh(client, true);
        }

        /// <summary>Recomputes the player's regions and sends them when they differ from the last send (or always, when forced).</summary>
        public void Refresh(Client client, bool force)
        {
            var player = client.Player;

            if (player.RegionsHeld && !force)
                return;

            var regionIds = RegionsAt(player.MapChannel, player.Position);

            if (!force && player.RegionIds != null && player.RegionIds.SequenceEqual(regionIds))
                return;

            player.RegionIds = regionIds;
            client.CallMethod(player.EntityId, new UpdateRegionsPacket(regionIds));
        }

        /// <summary>
        /// The sorted, distinct region ids whose enabled volumes contain the position. A map with
        /// no volumes at all falls back to map_info's base region, which is what was always sent
        /// before there were volumes.
        /// </summary>
        public List<uint> RegionsAt(MapChannel mapChannel, Vector3 position)
        {
            var result = new List<uint>();

            if (mapChannel == null)
                return result;

            if (!_byMap.TryGetValue(mapChannel.MapInfo.MapContextId, out var volumes) || volumes.Count == 0)
            {
                if (mapChannel.MapInfo.BaseRegionId != 0)
                    result.Add(mapChannel.MapInfo.BaseRegionId);

                return result;
            }

            var underground = NavMeshManager.IsUnderground(mapChannel, position);

            foreach (var volume in volumes)
                if (volume.Enabled && volume.Contains(position, underground) && !result.Contains(volume.RegionId))
                    result.Add(volume.RegionId);

            result.Sort();

            return result;
        }

        #region GM editing

        /// <summary>Creates the row and puts the volume live. Returns null when the insert failed.</summary>
        public MapRegion Add(MapRegion region)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();
            var entry = region.ToEntry();
            entry.Id = 0;

            var id = unitOfWork.MapRegions.AddMapRegion(entry);

            if (id == 0)
                return null;

            region.Id = id;
            Register(region);

            return region;
        }

        /// <summary>Writes the volume's current fields to its row.</summary>
        public bool Update(MapRegion region)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();

            return unitOfWork.MapRegions.UpdateMapRegion(region.ToEntry());
        }

        public bool Delete(MapRegion region)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateWorld();

            if (!unitOfWork.MapRegions.DeleteMapRegion(region.Id))
                return false;

            Unregister(region);

            return true;
        }

        /// <summary>The volumes on a map, nearest to the position first (those containing it come first, at distance 0).</summary>
        public List<MapRegion> OnMap(uint mapContextId, Vector3 position)
        {
            if (!_byMap.TryGetValue(mapContextId, out var volumes))
                return new List<MapRegion>();

            return volumes.OrderBy(v => v.Distance(position)).ThenBy(v => v.Id).ToList();
        }

        #endregion
    }
}
