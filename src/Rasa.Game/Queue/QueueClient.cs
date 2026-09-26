using System;
using System.Net;
using System.Net.Sockets;

namespace Rasa.Queue
{
    using Data;
    using Memory;
    using Networking;
    using Packets.Queue.Client;
    using Packets.Queue.Server;

    public class QueueClient
    {
        public QueueManager Manager { get; }
        public LengthedSocket Socket { get; }

        /// <summary>
        /// Read on the main loop by every QueueManager pass and written on the socket threads as
        /// the handshake advances, so the transitions go through <see cref="_clientLock"/>. They
        /// are single writes rather than read-modify-writes everywhere except MarkArrived, which
        /// is the one that has to be atomic: it is the main loop deciding a handed-off client has
        /// arrived at the world port, against the client's own thread closing the socket.
        /// </summary>
        public QueueState State { get; private set; }
        public uint UserId { get; set; }
        public uint OneTimeKey { get; set; }
        public DateTime EnqueueTime { get; private set; }
        public DateTime DequeueTime { get; set; }

        /// <summary>When the handoff was sent; the slot it holds is given up if nobody arrives.</summary>
        public DateTime RedirectTime { get; private set; }

        /// <summary>When the connection was accepted; a handshake that has not finished in time is closed.</summary>
        public DateTime ConnectedTime { get; } = DateTime.Now;

        /// <summary>
        /// Wires the socket up and nothing more: no I/O happens until <see cref="Start"/>. The
        /// manager builds this inside its client-list lock, and anything that touches the
        /// socket here can run this connection's handlers on the same thread - a receive that
        /// completes synchronously dispatches straight into the handshake, which goes on to
        /// Enqueue and from there to Server.Clients. Doing that under QueueManager.Clients
        /// nested the two list locks in the opposite order to the world loop, which holds
        /// Server.Clients for its whole tick and takes QueueManager.Clients in Arrived.
        /// </summary>
        public QueueClient(QueueManager manager, LengthedSocket socket)
        {
            Manager = manager;
            Socket = socket;
            Socket.OnReceive += OnReceive;
            Socket.OnError += OnError;
            Socket.OnDrop += OnDrop;

            State = QueueState.Authenticating;
        }

        /// <summary>
        /// Sends the server key and starts reading. Called by the manager once this client is on
        /// its list and the list's lock has been released; see the constructor for why that
        /// order matters.
        /// </summary>
        public void Start()
        {
            Socket.Send(new ServerKeyPacket
            {
                PublicKey = Manager.Config.PublicKey,
                Prime = Manager.Config.Prime,
                Generator = Manager.Config.Generator
            });

            // A send that could not go out has already closed the connection.
            if (State == QueueState.Disconnected)
                return;

            Socket.ReceiveAsync();
        }

        private void OnReceive(BufferData data)
        {
            // Runs on a socket completion thread: an exception escaping here is unhandled and
            // terminates the process, so a malformed or unexpected queue packet must only ever
            // cost this one connection.
            try
            {
                HandleReceive(data);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Error handling queue packet from {Socket.RemoteAddress}, disconnecting: {e}");
                Close();
            }
        }

        private void HandleReceive(BufferData data)
        {
            switch (State)
            {
                case QueueState.Authenticating:
                    var keyPacket = new ClientKeyPacket();

                    keyPacket.Read(data.GetReader());

                    if (keyPacket.PublicKey != Manager.Config.PublicKey)
                    {
                        Close();
                        return;
                    }

                    Socket.Send(new ClientKeyOkPacket());

                    SetState(QueueState.Authenticated);

                    break;

                case QueueState.Authenticated:
                    if (data[data.Offset++] != 7)
                        throw new Exception("Invalid opcode???");

                    var loginPacket = new QueueLoginPacket();

                    loginPacket.Read(data.GetReader());

                    // The account and key have to be a redirect the auth server has sent this
                    // game server, still waiting to be taken up at the world port. Nothing was
                    // checked: any connection could queue under any account id, hold a place and
                    // then a slot in the player count, and keep that account's real redirect
                    // session alive for as long as it stayed connected. The session is only read
                    // here; the world login still consumes it.
                    if (!Manager.Server.HasPendingLogin(loginPacket.UserId, loginPacket.OneTimeKey))
                    {
                        Logger.WriteLog(LogType.Security, $"Queue login from {Socket.RemoteAddress} for account {loginPacket.UserId} has no matching redirect session; closing.");
                        Close();
                        return;
                    }

                    UserId = loginPacket.UserId;
                    OneTimeKey = loginPacket.OneTimeKey;

                    // One place in the queue per account: an earlier connection for it that has
                    // not reached the world yet is a client that reconnected, or a copy.
                    Manager.CloseEarlierConnections(this);

                    SetState(QueueState.InQueue);

                    Manager.Enqueue(this);
                    EnqueueTime = DateTime.Now;
                    break;

                default:
                    throw new Exception("Received packet in a invalid queue state!");
            }
        }

        private readonly object _clientLock = new object();

        /// <summary>
        /// Advances the handshake, unless this connection has already gone. A socket thread that
        /// is mid-handshake when the main loop or the communicator closes the connection would
        /// otherwise put it back into a live state and leave it in the queue holding a slot, with
        /// a socket nobody can write to.
        /// </summary>
        private void SetState(QueueState state)
        {
            lock (_clientLock)
            {
                if (State == QueueState.Disconnected)
                    return;

                State = state;
            }
        }

        private void OnError(SocketAsyncEventArgs args)
        {
            Close();
        }

        /// <summary>The socket gave up on this connection; the reason is already logged.</summary>
        private void OnDrop(string reason)
        {
            Close();
        }

        /// <summary>
        /// Reached from three threads: this connection's own socket threads (OnError, OnDrop and
        /// the receive handler's catch), the main loop (QueueManager expiring a redirect that
        /// nobody arrived for), and the auth communicator's thread (an account locked while it
        /// was waiting). Nothing used to stop two of them running the whole teardown, and the
        /// socket was closed before the state said so, leaving a window in which the main loop's
        /// next pass would write a position update to a socket that had already gone.
        /// </summary>
        public void Close()
        {
            lock (_clientLock)
            {
                if (State == QueueState.Disconnected)
                    return;

                State = QueueState.Disconnected;
            }

            Socket.Close();

            Manager.Disconnect(this);
        }

        /// <summary>
        /// The account this connection was handed off for has logged in at the world port. From
        /// here it is a world client and counted as one; whether the client closes this socket
        /// now or keeps it open until it exits, it no longer holds a slot of its own. Before this
        /// a client that kept the queue socket open counted twice, and the server read as full
        /// at half its cap.
        /// </summary>
        internal void MarkArrived()
        {
            lock (_clientLock)
                if (State == QueueState.Redirecting)
                    State = QueueState.Arrived;
        }

        public void Redirect(IPAddress ip, int port)
        {
            SetState(QueueState.Redirecting);
            RedirectTime = DateTime.Now;

            Socket.Send(new HandoffToGamePacket
            {
                OneTimeKey = OneTimeKey,
                ServerIp = ip,
                ServerPort = port,
                UserId = UserId
            });
        }

        public void SendPositionUpdate(int position, int estimatedTime)
        {
            Socket.Send(new QueuePositionPacket
            {
                Position = position,
                EstimatedTime = estimatedTime
            });
        }
    }
}
