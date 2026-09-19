using System;
using System.Net;
using System.Net.Sockets;

namespace Rasa.Auth
{
    using Data;
    using Memory;
    using Networking;
    using Packets;
    using Packets.Communicator;

    public class CommunicatorClient
    {
        public LengthedSocket Socket { get; }
        public Server Server { get; }
        public byte ServerId { get; set; }
        public int QueuePort { get; set; }
        public int GamePort { get; set; }
        public byte AgeLimit { get; set; }
        public byte PKFlag { get; set; }
        public ushort CurrentPlayers { get; set; }
        public ushort MaxPlayers { get; set; }
        public DateTime LastRequestTime { get; set; }
        public IPAddress PublicAddress { get; set; }

        private readonly PacketRouter<CommunicatorClient, CommOpcode> _router = new PacketRouter<CommunicatorClient, CommOpcode>();

        public bool Connected => Socket.Connected;

        public CommunicatorClient(LengthedSocket socket, Server server)
        {
            Server = server;
            Socket = socket;

            Socket.OnReceive += OnReceive;
            Socket.OnError += OnError;

            Socket.ReceiveAsync();
        }

        /// <summary>
        /// Socket completion thread, carrying messages from a game server. A malformed body or a
        /// throwing handler must not end the auth process, and it must not silently cost the link
        /// to that game server either - which is what happened when the exception was left to the
        /// socket layer's catch-all, whose log line names the socket operation rather than the
        /// message that could not be handled.
        /// </summary>
        private void OnReceive(BufferData data)
        {
            CommOpcode? opcode = null;

            try
            {
                opcode = (CommOpcode) data.Buffer[data.BaseOffset + data.Offset++];

                var packetType = _router.GetPacketType(opcode.Value);
                if (packetType == null)
                    return;

                var packet = Activator.CreateInstance(packetType) as IOpcodedPacket<CommOpcode>;
                if (packet == null)
                    return;

                packet.Read(data.GetReader());

                _router.RoutePacket(this, packet);
            }
            catch (Exception e)
            {
                var what = opcode.HasValue ? $"a {opcode.Value} message" : "a message whose opcode could not be read";
                Logger.WriteLog(LogType.Error, $"Error handling {what} from game server {ServerId}: {e}");
            }
        }

        private void OnError(SocketAsyncEventArgs args)
        {
            Socket.Close();

            Server.DisconnectCommunicator(this);
        }

        public void RequestServerInfo()
        {
            LastRequestTime = DateTime.Now;

            Socket.Send(new ServerInfoRequestPacket());
        }

        public void RequestRedirection(Client client)
        {
            Socket.Send(new RedirectRequestPacket
            {
                AccountId = client.AccountEntry.Id,
                Email = client.AccountEntry.Email,
                Username = client.AccountEntry.Username,
                OneTimeKey = client.OneTimeKey
            });
        }

        // ReSharper disable once UnusedMember.Local
        [PacketHandler(CommOpcode.LoginRequest)]
        private void MsgLoginRequest(LoginRequestPacket packet)
        {
            // Set before the slot is claimed, not after. DisconnectCommunicator gives the slot
            // back only when ServerId is non-zero, and this used to be assigned four statements
            // and two sends later - so a game server whose socket died in that window left its
            // entry in GameServers forever, pointing at a dead connection. It could then never
            // reconnect (the slot reads as in use) and the auth server went on asking that dead
            // socket for its player counts once a second until it was restarted.
            ServerId = packet.ServerId;
            PublicAddress = packet.PublicAddress;

            if (!Server.AuthenticateGameServer(packet, this))
            {
                ServerId = 0;

                Socket.Send(new LoginResponsePacket
                {
                    Response = CommLoginReason.Failure
                });
                return;
            }

            Socket.Send(new LoginResponsePacket
            {
                Response = CommLoginReason.Success
            });

            RequestServerInfo();
        }

        // ReSharper disable once UnusedMember.Local
        [PacketHandler(CommOpcode.ServerInfoResponse)]
        private void MsgGameInfoResponse(ServerInfoResponsePacket packet)
        {
            Server.UpdateServerInfo(this, packet);
        }

        // ReSharper disable once UnusedMember.Local
        [PacketHandler(CommOpcode.RedirectResponse)]
        private void MsgRedirectResponse(RedirectResponsePacket packet)
        {
            Server.RedirectResponse(this, packet);
        }
    }
}
