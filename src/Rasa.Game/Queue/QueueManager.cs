using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;

namespace Rasa.Queue
{
    using Config;
    using Data;
    using Game;
    using Networking;

    public delegate void RedirectDelegate(QueueClient client);

    public class QueueManager
    {
        private readonly Queue<QueueClient> _queuedClients = new Queue<QueueClient>();

        public List<QueueClient> Clients { get; } = new List<QueueClient>();

        public Server Server { get; }
        public LengthedSocket Socket { get; }
        public int QueuedClients => _queuedClients.Count;

        /// <summary>Queue connections handed off to the world port that are still open.</summary>
        public int RedirectingClients
        {
            get
            {
                lock (Clients)
                    return Clients.Count(c => c.State == QueueState.Redirecting);
            }
        }

        /// <summary>
        /// How long a handed-off client keeps its slot while nobody logs in at the world port.
        /// The client connects there straight away, before it loads anything, so this is
        /// generous; a redirect session on the world side lasts the same minute.
        /// </summary>
        private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// The account has logged in at the world port: its queue connection, if still open,
        /// stops counting as a slot.
        /// </summary>
        public void Arrived(uint userId)
        {
            lock (Clients)
                foreach (var client in Clients)
                    if (client.UserId == userId && client.State == QueueState.Redirecting)
                        client.MarkArrived();
        }

        /// <summary>
        /// Closes handed-off connections whose account never turned up at the world port, so a
        /// client that took the handoff and went away does not hold a slot for as long as it
        /// keeps the socket open.
        /// </summary>
        private void ExpireRedirects()
        {
            List<QueueClient> expired;
            var cutoff = DateTime.Now - RedirectTimeout;

            lock (Clients)
                expired = Clients.Where(c => c.State == QueueState.Redirecting && c.RedirectTime < cutoff).ToList();

            // QueueClient.Close removes the client from Clients, so close outside the lock.
            foreach (var client in expired)
            {
                Logger.WriteLog(LogType.Network, $"Queue client for account {client.UserId} was handed off {RedirectTimeout.TotalSeconds:F0} s ago and never logged in; closing it.");
                client.Close();
            }
        }

        /// <summary>Closes every queue connection belonging to an account.</summary>
        public void Disconnect(uint userId)
        {
            List<QueueClient> matches;

            lock (Clients)
                matches = Clients.Where(c => c.UserId == userId && c.State != QueueState.Disconnected).ToList();

            // QueueClient.Close removes the client from Clients, so close outside the lock.
            foreach (var client in matches)
                client.Close();
        }

        /// <summary>Accounts with a live, authenticated queue connection.</summary>
        public HashSet<uint> ConnectedUserIds()
        {
            lock (Clients)
                return new HashSet<uint>(Clients
                    .Where(c => c.State == QueueState.Authenticated || c.State == QueueState.InQueue || c.State == QueueState.Redirecting)
                    .Select(c => c.UserId));
        }
        public RedirectDelegate OnRedirect { get; set; }
        public QueueConfig Config => Server.Config.QueueConfig;

        public QueueManager(Server server)
        {
            Server = server;

            Socket = new LengthedSocket(SizeType.Dword, false);
            Socket.OnError += OnError;
            Socket.OnAccept += OnAccept;
            Socket.Bind(new IPEndPoint(IPAddress.Any, Config.Port));
            Socket.Listen(Config.Backlog);

            Socket.AcceptAsync();
        }

        private static void OnError(SocketAsyncEventArgs args)
        {
            if (args.LastOperation == SocketAsyncOperation.Accept && args.AcceptSocket != null && args.AcceptSocket.Connected)
                args.AcceptSocket.Shutdown(SocketShutdown.Both);
        }

        private void OnAccept(LengthedSocket socket)
        {
            Socket.AcceptAsync();

            lock (Clients)
                Clients.Add(new QueueClient(this, socket));
        }

        public void Disconnect(QueueClient client)
        {
            lock (Clients)
                Clients.Remove(client);
        }

        public void Enqueue(QueueClient client)
        {
            lock (_queuedClients)
            {
                if (!Server.IsFull)
                {
                    // TODO: need a position update before redirect?
                    client.Redirect(Server.PublicAddress, Server.Config.GameConfig.Port);
                    return;
                }

                _queuedClients.Enqueue(client);

                var pos = 0;

                foreach (var c in _queuedClients)
                {
                    if (c.State == QueueState.Disconnected)
                        continue;

                    if (c == client)
                        c.SendPositionUpdate(pos, 10000 * pos); // TODO: proper estimated time calculation
                    else
                        ++pos;
                }
            }
        }

        private void AdvanceQueue(int freeSlots)
        {
            if (QueuedClients == 0)
                return;

            lock (_queuedClients)
            {
                for (var i = 0; i < freeSlots && QueuedClients > 0;)
                {
                    var client = _queuedClients.Dequeue();
                    if (client.State == QueueState.Disconnected)
                        continue;

                    client.Redirect(Server.PublicAddress, Server.Config.GameConfig.Port);
                    ++i;
                }
            }
        }

        private void ClearDisconnected()
        {
            if (QueuedClients == 0)
                return;

            lock (_queuedClients)
            {
                do
                {
                    var c = _queuedClients.Peek();
                    if (c.State != QueueState.Disconnected)
                        break;

                    _queuedClients.Dequeue();
                }
                while (QueuedClients > 0);
            }
        }

        public void Update(int freeSlots)
        {
            ExpireRedirects();

            if (QueuedClients == 0)
                return;

            lock (_queuedClients)
            {
                ClearDisconnected();

                AdvanceQueue(freeSlots);

                var position = 0;

                foreach (var client in _queuedClients)
                    if (client.State != QueueState.Disconnected)
                        client.SendPositionUpdate(position, 10000 * position++); // TODO: proper estimated time calculation
            }
        }
    }
}
