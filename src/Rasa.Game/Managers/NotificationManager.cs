using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// Notification (client/clientmethod.py Recv_Notification): on-screen timers, object
    /// animations and world audio. It is called on the client method entity, not on the entity
    /// the notification is about, so the cell broadcasts here are their own rather than
    /// CellManager's, which address the origin entity.
    /// </summary>
    public class NotificationManager
    {
        private static NotificationManager _instance;
        private static readonly object InstanceLock = new object();

        public static NotificationManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new NotificationManager();
                    }
                }

                return _instance;
            }
        }

        private NotificationManager()
        {
        }

        public void Send(Client client, NotificationPacket packet)
        {
            client?.CallMethod(SysEntity.ClientMethodId, packet);
        }

        /// <summary>Everyone on the map, for something that is not tied to a place.</summary>
        public void SendToMap(MapChannel mapChannel, NotificationPacket packet)
        {
            if (mapChannel == null)
                return;

            foreach (var client in mapChannel.ClientList)
                if (client.State == ClientState.Ingame)
                    Send(client, packet);
        }

        /// <summary>
        /// Everyone whose cells cover the position, for something happening at a place: an object
        /// animation or positional audio is worth sending only to players who can see the entity.
        /// </summary>
        public void SendToCells(MapChannel mapChannel, Vector3 position, NotificationPacket packet)
        {
            if (mapChannel == null)
                return;

            var cellPosX = (uint)(position.X / CellManager.CellSize + CellManager.CellBias);
            var cellPosZ = (uint)(position.Z / CellManager.CellSize + CellManager.CellBias);
            var cellMatrix = CellManager.Instance.CreateCellMatrix(mapChannel, cellPosX, cellPosZ);
            var notified = new HashSet<Client>();

            foreach (var cellSeed in cellMatrix)
                foreach (var client in mapChannel.MapCellInfo.Cells[cellSeed].ClientList)
                    if (client.State == ClientState.Ingame && notified.Add(client))
                        Send(client, packet);
        }

        #region Timers

        public void DisplayTimer(Client client, TimerType timerType, int currentValue, bool countdown)
        {
            Send(client, NotificationPacket.DisplayTimer(timerType, currentValue, true, countdown));
        }

        public void PauseTimer(Client client, TimerType timerType, int currentValue, bool countdown)
        {
            Send(client, NotificationPacket.DisplayTimer(timerType, currentValue, false, countdown));
        }

        public void StopTimer(Client client)
        {
            Send(client, NotificationPacket.StopTimer());
        }

        #endregion

        #region World audio and animation

        public void PlayObjectAnimation(MapChannel mapChannel, Vector3 position, ulong targetEntityId, uint animationSpecId)
        {
            SendToCells(mapChannel, position, NotificationPacket.ObjectAnimation(targetEntityId, animationSpecId));
        }

        public void StopObjectAnimation(MapChannel mapChannel, Vector3 position, ulong targetEntityId)
        {
            SendToCells(mapChannel, position, NotificationPacket.StopObjectAnimation(targetEntityId));
        }

        public void PlayLocationAudio(MapChannel mapChannel, Vector3 position, ulong targetEntityId, uint audioSpecId)
        {
            SendToCells(mapChannel, position, NotificationPacket.LocationAudio(targetEntityId, audioSpecId));
        }

        public void StopLocationAudio(MapChannel mapChannel, Vector3 position, ulong targetEntityId)
        {
            SendToCells(mapChannel, position, NotificationPacket.StopLocationAudio(targetEntityId));
        }

        public void PlayBackgroundAudio(Client client, uint audioSpecId)
        {
            Send(client, NotificationPacket.BackgroundAudio(audioSpecId));
        }

        #endregion
    }
}
