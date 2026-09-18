using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Extensions;
    using Game;
    using Packets.MapChannel.Server;
    using Structures;

    public class MapTriggerManager
    {
        private static MapTriggerManager _instance;
        private static readonly object InstanceLock = new object();
        public static MapTriggerManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new MapTriggerManager();
                    }
                }

                return _instance;
            }
        }

        public MapTriggerManager()
        {
        }

        internal void MapTriggerInit()
        {
        }

        /// <summary>
        /// A player has walked onto a dropship pad: they gain it, the first time, the way a
        /// waypoint is gained, and the travel window opens with the pads they can fly to.
        /// </summary>
        internal void PlayerEnterTriggerRange(Client client, MapTrigger mapTrigger)
        {
            if (client.Player.IsNear5m(mapTrigger))
            {
                if (mapTrigger.TriggeredBy.Contains(client))
                    return;

                mapTrigger.TriggeredBy.Add(client);

                DynamicObjectManager.Instance.CheckPlayerWaypoint(client, new WaypointInfo(mapTrigger.TriggerId, false, WaypointType.Dropship));

                var dropshipInfoList = DynamicObjectManager.Instance.CreateListOfDropships(client, mapTrigger.TriggerId);

                client.CallMethod(SysEntity.ClientMethodId, new EnteredWaypointPacket(mapTrigger.MapContextId, mapTrigger.MapContextId, dropshipInfoList, WaypointType.Dropship, mapTrigger.TriggerId));
            }
        }
        internal void PlayerExitTriggerRange(Client client, MapTrigger mapTrigger)
        {
            if (!client.Player.IsNear5m(mapTrigger))
                if (mapTrigger.TriggeredBy.Contains(client))
                {
                    mapTrigger.TriggeredBy.Remove(client);
                    client.CallMethod(SysEntity.ClientMethodId, new ExitedWaypointPacket());
                }
        }

        internal void TriggersProximityWorker(MapChannel mapChannel)
        {
            foreach (var client in mapChannel.ClientList)
            {
                if (client.Player.Disconected || client.Player == null || client.State == ClientState.Loading)
                    continue;

                // A matrix that names no cell of this map is a player who is not standing in it
                // yet, not a reason to throw out of the worker and cost every map after this one
                // its tick.
                // A trigger is filed under the cell its centre is in, and a player 5 m from a
                // pad's centre can be standing in the next cell over - the cells are 25.6 m and
                // pads sit where they sit. Looking only at the player's own cell meant a pad
                // that worked from one side and not the other.
                foreach (var cell in CellManager.CellsIn(mapChannel, client.Player.Cells))
                    foreach (var mapTrigger in cell.MapTriggers)
                    {
                        // check for players that enter range
                        PlayerEnterTriggerRange(client, mapTrigger);

                        // check for players that leave range
                        PlayerExitTriggerRange(client, mapTrigger);
                    }
            }
        }
    }
}
