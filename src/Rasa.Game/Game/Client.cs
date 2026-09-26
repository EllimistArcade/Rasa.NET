using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Rasa.Game
{
    using Cryptography;
    using Data;
    using Handlers;
    using Managers;
    using Memory;
    using Models;
    using Networking;
    using Packets;
    using Packets.Protocol;
    using Rasa.Packets.Communicator.Both;
    using Repositories.Char;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;
    using System.Net.Mail;
    using System.Numerics;

    public class Client
    {
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;

        public const int LengthSize = 2;

        public Server Server { get; private set; }
        public LengthedSocket Socket { get; private set; }
        public ClientCryptData Data { get; private set; }
        public GameAccountEntry AccountEntry { get; private set; }
        public uint LoadingMap { get; set; }

        /// <summary>
        /// A Wonkavate has gone to this client and the MapLoaded that answers it has not come
        /// back. The client sends MapLoaded once per load, when its loading screen ends
        /// (wonkavator.py HandleLoadingScreenEnd), so each Wonkavate allows exactly one. Set by
        /// everything that sends one - PassClientToMapInstance, ChangeMap, a departing dropship -
        /// and cleared by MapChannelManager.MapLoaded.
        /// </summary>
        internal bool AwaitingMapLoaded { get; set; }

        /// <summary>
        /// EnableDevCommands has gone to this connection. What it switches on lives in the client
        /// process, which a map change or a trip to the character screen does not restart, so it
        /// is sent once; see MapChannelManager.MapLoaded.
        /// </summary>
        internal bool DevCommandsSent { get; set; }

        public ClientState State { get; set; }
        public Manifestation Player = new();
        public Movement Movement { get; set; }
        public uint[] SendSequence { get; } = new uint[256];
        public uint[] ReceiveSequence { get; } = new uint[256];
        public List<UserOptions> UserOptions = new();

        private readonly object _clientLock = new();
        private readonly ClientPacketHandler _handler;
        private readonly PacketQueue _packetQueue = new();

        // Inbound byte stream. Owned exclusively by the MainLoop thread: it is only ever
        // touched from Update()/TryDecodeNextPacket() and (after disconnect) Close().
        private readonly NonContiguousMemoryStream _incomingDataQueue = new();

        // Hand-off from socket completion threads to the MainLoop. OnReceive() runs on an
        // IOCP thread and must not mutate _incomingDataQueue (its backing List<> is not
        // thread-safe; the MainLoop enumerates and RemoveRange()s it while decoding). It
        // rents a pooled array, copies the chunk in, and enqueues it here; Update() drains
        // the queue into the stream on the MainLoop. Receives are serialized per socket,
        // so there is exactly one producer per client and byte order is preserved.
        private readonly ConcurrentQueue<(byte[] Buffer, int Length)> _pendingChunks = new();

        // Bytes enqueued but not yet drained. Bounded so a client that floods faster than
        // the MainLoop consumes cannot grow memory without limit. Legitimate traffic is a
        // few KB/s and a single frame is capped at 8 KB by LengthedSocket, so this only
        // trips on a misbehaving client or a MainLoop stalled for many seconds.
        private int _pendingBytes;
        private const int MaxPendingBytes = 512 * 1024;

        /// <summary>
        /// Packets handled for this connection in one tick. The rest wait in the inbound stream
        /// for the next one, so a client that sends faster than this falls behind and, once that
        /// backlog passes MaxPendingBytes, is disconnected. The real client sends a handful of
        /// packets a tick - movement at most every one - so this is far above anything it does.
        /// </summary>
        private const int MaxPacketsPerTick = 64;


        private static PacketRouter<ClientPacketHandler, GameOpcode> PacketRouter { get; } = new PacketRouter<ClientPacketHandler, GameOpcode>();

        public static Type GetPacketType(GameOpcode opcode)
        {
            return PacketRouter.GetPacketType(opcode);
        }

        public Client(
            IGameUnitOfWorkFactory gameUnitOfWorkFactory,
            ClientPacketHandler handler)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;

            _handler = handler;
            _handler.RegisterClient(this);
        }

        /// <summary>When this connection finished the key exchange; it has a short while to log in.</summary>
        internal DateTime ConnectedTime { get; private set; }

        public void RegisterAtServer(Server server, LengthedSocket socket, ClientCryptData cryptData)
        {
            ConnectedTime = DateTime.UtcNow;
            Socket = socket;
            Data = cryptData;
            Server = server;

            State = ClientState.Connected;

            Socket.OnError += OnError;
            Socket.OnDrop += OnDrop;
            Socket.OnReceive += OnReceive;
            Socket.OnEncrypt += OnEncrypt;
            Socket.OnDecrypt += OnDecrypt;

            Socket.ReceiveAsync();

            for (var i = 0; i < 256; ++i)
                SendSequence[i] = 1;

            Logger.WriteLog(LogType.Network, "*** Client connected from {0}", Socket.RemoteAddress);
        }

        public void Update(long delta)
        {
            // Nothing in this method may throw: Update() is driven by the single MainLoop
            // thread that services every client and every manager. An escaping exception
            // takes the whole world down, not just this connection.
            try
            {
                DrainPendingChunks();

                // A backlog this client has built up faster than MaxPacketsPerTick lets it be read.
                if (_incomingDataQueue.Length > MaxPendingBytes)
                {
                    Logger.WriteLog(LogType.Security, $"Client {Socket.RemoteAddress} has {_incomingDataQueue.Length} bytes of unread input (limit {MaxPendingBytes}), disconnecting.");
                    Close();
                    return;
                }

                var handled = 0;

                foreach (var protocolPacket in DecodeIncomingPackets())
                {
                    // Stopping the enumeration leaves everything after this packet in the stream.
                    if (++handled > MaxPacketsPerTick)
                        break;

                    try
                    {
                        HandleProtocolPacket(protocolPacket);
                    }
                    catch (InvalidClientMessageException)
                    {
                        Close();
                        return;
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLog(LogType.Error, $"Error handling {protocolPacket.Type} from {Socket.RemoteAddress}, disconnecting client: {e}");
                        Close();
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                // DecodeIncomingPackets() can throw while advancing the iterator (desynced
                // or malformed stream), which the inner try above would never see.
                Logger.WriteLog(LogType.Error, $"Error decoding packet stream from {Socket.RemoteAddress}, disconnecting client: {e}");
                Close();
                return;
            }

            try
            {
                IBasePacket packet;

                while ((packet = _packetQueue.PopOutgoing()) != null)
                    SendPacket(packet);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Error sending queued packets to {Socket.RemoteAddress}, disconnecting client: {e}");
                Close();
            }
        }

        public void Close(bool sendPacket = true)
        {
            if (State == ClientState.Disconnected)
                return;

            lock (_clientLock)
            {
                if (State == ClientState.Disconnected)
                    return;

                Logger.WriteLog(LogType.Network, "*** Client disconnected! Ip: {0}", Socket.RemoteAddress);

                State = ClientState.Disconnected;

                Socket.Close();

                Server.Disconnect(this);

                // A dropped connection (Alt+F4, crash, network loss) never runs the /logout
                // flow, and that flow was the only thing that set RemoveFromMap - so the
                // character stayed in its map cell as a frozen copy, visible to everyone
                // including the same player on their next login. Flag it for the
                // MapChannelWorker instead of calling RemovePlayer here: Close() is also
                // reached from socket completion threads, and RemovePlayer walks the cell
                // and entity tables the MainLoop owns. Disconected goes first so the
                // worker skips handing a dead socket back to character selection, and so
                // the visibility and trigger passes stop treating the player as present.
                if (Player != null && Player.MapChannel != null)
                {
                    Player.Disconected = true;
                    Player.RemoveFromMap = true;
                }

                DiscardPendingChunks();

                try
                {
                    SaveCharacter();
                }
                catch (Exception e)
                {
                    // Close() is reached from socket completion threads (OnError) as well as
                    // from the MainLoop. A throw here used to terminate the process on every
                    // disconnect that happened before a character was loaded.
                    Logger.WriteLog(LogType.Error, $"Failed to save character on disconnect: {e}");
                }
            }
        }

        public void CallMethod(ulong entityId, PythonPacket packet)
        {
            SendMessage(new CallMethodMessage(entityId, packet));
        }

        public void CallMethod(SysEntity entityId, PythonPacket packet)
        {
            SendMessage(new CallMethodMessage((ulong)entityId, packet));
        }

        internal void MoveObject(ulong entityId, Movement movement)
        {
            SendMessage(new MoveObjectMessage(entityId, movement), false, 1);
        }

        // Cell Domain
        public void CellCallMethod(Client client, ulong entityId, PythonPacket packet)
        {
           var clientList = new List<Client>();

            foreach (var cellSeed in client.Player.Cells)
                if (client.Player.MapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                    clientList.AddRange(cell.ClientList);

            foreach (var tempClient in clientList)
                tempClient.CallMethod(entityId, packet);
        }

        // Cell Domain ignore self
        public void CellIgnoreSelfCallMethod(Client client, PythonPacket packet)
        {
            var clientList = new List<Client>();

            foreach (var cellSeed in client.Player.Cells)
                if (client.Player.MapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                    clientList.AddRange(cell.ClientList);

            foreach (var tempClient in clientList)
            {
                if (tempClient == client)
                    continue;

                tempClient.CallMethod(client.Player.EntityId, packet);
            }
        }

        // Cell send movement
        internal void CellMoveObject(Client client, MoveObjectMessage moveObjectMessage, bool ignoreSelf)
        {
            var clientList = new List<Client>();

            // A cell the player's matrix names but the map does not have is a stale matrix, not
            // a reason to drop the connection; whoever is in the other cells still gets the move.
            foreach (var cellSeed in client.Player.Cells)
                if (client.Player.MapChannel.MapCellInfo.Cells.TryGetValue(cellSeed, out var cell))
                    clientList.AddRange(cell.ClientList);

            foreach (var tempClient in clientList)
            {
                if (tempClient == client && ignoreSelf)
                    continue;

                tempClient.SendMessage(moveObjectMessage, false, 1);
            }
        }

        public void SendMessage(IClientMessage message, bool compress = false, byte channel = 0, bool delay = true)
        {
            var protocolPacket = new ProtocolPacket(message, message.Type, compress, channel);

            if (!delay)
                SendPacket(protocolPacket);
            else
                _packetQueue.EnqueueOutgoing(protocolPacket);
        }

        public void SendPacket(IBasePacket packet)
        {
            var pPacket = packet as ProtocolPacket;
            if (pPacket == null)
            {
                Logger.WriteLog(LogType.Error, $"SendPacket() called with a non-ProtocolPacket ({packet?.GetType().Name ?? "null"}), dropping it.");
                return;
            }

            if (pPacket.Channel != 0)
                pPacket.SequenceNumber = SendSequence[pPacket.Channel]++;

            Socket.Send(pPacket);
        }

        private void HandleProtocolPacket(ProtocolPacket protocolPacket)
        {
            switch (protocolPacket.Type)
            {
                case ClientMessageOpcode.Login:
                    // Once per connection, as its first message. Nothing checked this: a second
                    // Login from a connection already at the character screen or in the world ran
                    // the whole login again - a new account entry and character selection - over a
                    // manifestation still registered and standing in its map's cells, and the
                    // already-logged-in check only looks at other connections. The client sends it
                    // once, straight after the key exchange, so anything else is not the client.
                    if (State != ClientState.Connected)
                    {
                        Logger.WriteLog(LogType.Security,
                            $"Client {Socket.RemoteAddress} (account {AccountEntry?.Id}) sent a second Login in state {State}; disconnecting.");
                        Close();
                        return;
                    }

                    var loginMsg = GetMessageAs<LoginMessage>(protocolPacket);

                    if (loginMsg.Version.Length != 8 || loginMsg.Version != "1.16.5.0")
                    {
                        Logger.WriteLog(LogType.Error, $"Client version mismatch: Server: 1.16.5.0 | Client: {loginMsg.Version}");

                        SendMessage(new LoginResponseMessage
                        {
                            ErrorCode = LoginErrorCodes.VersionMismatch,
                            Subtype = LoginResponseMessageSubtype.Failed
                        }, delay: false);

                        LoginFailed();
                        return;
                    }

                    var loginEntry = Server.AuthenticateClient(this, loginMsg.AccountId, loginMsg.OneTimeKey);
                    if (loginEntry == null)
                    {
                        Logger.WriteLog(LogType.Error, "Client with ip: {0} tried to log in with invalid session data! User Id: {1} | OneTimeKey: {2}", Socket.RemoteAddress, loginMsg.AccountId, loginMsg.OneTimeKey);

                        SendMessage(new LoginResponseMessage
                        {
                            ErrorCode = LoginErrorCodes.AuthenticationFailed,
                            Subtype = LoginResponseMessageSubtype.Failed
                        }, delay: false);

                        LoginFailed();
                        return;
                    }

                    using (var unitOfWork = _gameUnitOfWorkFactory.CreateChar())
                    {
                        // A new account is created at level 0, an ordinary player. Logging in used
                        // to set every account to level 1, which made the GM check in front of the
                        // dot commands true for everyone who could reach it. Levels are handed out
                        // from the Game console now: gm <familyName> <level>.
                        unitOfWork.GameAccounts.CreateOrUpdate(loginEntry.Id, loginEntry.Name, loginEntry.Email);

                        if (Server.IsBanned(loginMsg.AccountId))
                        {
                            Logger.WriteLog(LogType.Error, "Client with ip: {0} tried to log in while the account is banned! User Id: {1}", Socket.RemoteAddress, loginMsg.AccountId);

                            SendMessage(new LoginResponseMessage
                            {
                                ErrorCode = LoginErrorCodes.AccountLocked,
                                Subtype = LoginResponseMessageSubtype.Failed
                            }, delay: false);

                            LoginFailed();
                            return;
                        }

                        if (Server.IsAlreadyLoggedIn(loginMsg.AccountId))
                        {
                            Logger.WriteLog(LogType.Error, "Client with ip: {0} tried to log in while the account is being played on! User Id: {1}", Socket.RemoteAddress, loginMsg.AccountId);

                            SendMessage(new LoginResponseMessage
                            {
                                ErrorCode = LoginErrorCodes.AlreadyLoggedIn,
                                Subtype = LoginResponseMessageSubtype.Failed
                            }, delay: false);

                            LoginFailed();
                            return;
                        }

                        LoadGameAccountEntry(unitOfWork, loginEntry.Id);

                        unitOfWork.GameAccounts.UpdateLoginData(loginEntry.Id, Socket.RemoteAddress);
                        unitOfWork.Complete();
                    }

                    SendMessage(new LoginResponseMessage
                    {
                        AccountId = loginMsg.AccountId,
                        Subtype = LoginResponseMessageSubtype.Success
                    });

                    State = ClientState.LoggedIn;

                    CharacterManager.Instance.StartCharacterSelection(this);
                    break;

                case ClientMessageOpcode.Move:
                    if (Player == null)
                    {
                        return;
                    }

                    var moveMessage = GetMessageAs<MoveMessage>(protocolPacket);
                    if (moveMessage.Movement == null)
                    {
                        return;
                    }

                    // Only a player who is in the world moves in it. Between a map change and the
                    // client's MapLoaded the character already points at the new map and its
                    // arrival position, while the client's last few Move packets - sent before it
                    // saw PreWonkavate - are still arriving with old-map coordinates. Applying one
                    // overwrote the arrival position, and relaying it indexed the new map's cell
                    // table with the old map's cells, which threw and cost the player the
                    // connection every time they walked through a pass.
                    if (State != ClientState.Ingame)
                        return;

                    // Where the client says it is, believed only as far as the character could
                    // have walked since the last one. A refused Move is dropped and the client is
                    // put back; everything below it reads Position as the truth.
                    if (!ManifestationManager.Instance.AcceptMove(this, moveMessage.Movement))
                        return;

                    var movedFrom = Player.Position;

                    Player.Position = moveMessage.Movement.Position;

                    // A fall, if this Move ended one (FallDamage), and the flags for a GM watching them.
                    FallDamage.OnMove(this, movedFrom, Player.Position, Environment.TickCount64);
                    FallDamage.ShowMoveFlags(this, moveMessage, movedFrom);
                    Player.Rotation = moveMessage.Movement.ViewDirection.X;
                    Movement = moveMessage.Movement;

                    ManifestationManager.Instance.NotifyPlayerActivity(this);

                    // send your movement to other players in visibility range
                    var moveObjectMessage = new MoveObjectMessage(Player.EntityId, moveMessage.Movement);
                    CellMoveObject(this, moveObjectMessage, true);

                    break;

                case ClientMessageOpcode.CallServerMethod:
                    var csmPacket = GetMessageAs<CallServerMethodMessage>(protocolPacket);

                    if (!csmPacket.ReadPacket())
                    {
                        Close(true);
                        return;
                    }

                    // Nothing that acts on the world runs for a connection that is not in it.
                    if (!IsExpected(csmPacket.MethodId, csmPacket.Packet))
                        return;

                    // Methods whose every call goes out to everyone nearby, at the rate a person
                    // could use them.
                    if (!WithinRate(csmPacket.MethodId))
                        return;

                    // MethodId, not Packet.Opcode: an opcode with no handler leaves Packet null.
                    ManifestationManager.Instance.NotifyPlayerActivity(this, csmPacket.MethodId);

                    PacketRouter.RoutePacket(_handler, csmPacket.Packet);
                    break;

                case ClientMessageOpcode.Ping:
                    var pingMessage = GetMessageAs<PingMessage>(protocolPacket);

                    SendMessage(pingMessage, delay: false);
                    break;
            }
        }

        /// <summary>
        /// The character is in the world: registered with the EntityManager, standing in a map's
        /// cells, and holding inventory lists that name live entities. Teleporting counts - a
        /// dropship ride keeps the manifestation and everything registered with it - but Loading
        /// does not, because a map change tears all of that down and MapLoaded builds it again.
        /// </summary>
        public bool IsInWorld => State == ClientState.Ingame || State == ClientState.Teleporting;

        /// <summary>
        /// The methods the real client sends while it is not in the world, taken from the module
        /// each one is sent from: character creation and selection
        /// (client/inputstate/charactercreation.py, characterselection.py), the loading screen's
        /// MapLoaded (wonkavator.py), the ping it keeps up throughout (game.py), and the
        /// account-wide options it can save from anywhere (clientmethod.py).
        ///
        /// Everything else names an entity, a map, a character or a clan, and only means
        /// anything while the character is in the world. SaveCharacterOptions is deliberately
        /// absent: it writes rows keyed on the character id, which is 0 until one is chosen.
        /// </summary>
        private static readonly HashSet<GameOpcode> WorldlessMethods = new()
        {
            GameOpcode.RequestCharacterName,
            GameOpcode.RequestFamilyName,
            GameOpcode.RequestCreateCharacterInSlot,
            GameOpcode.CreateCharacter,
            GameOpcode.RequestCloneCharacterToSlot,
            GameOpcode.RequestDeleteCharacterInSlot,
            GameOpcode.RequestSwitchToCharacterInSlot,
            GameOpcode.StoreUserClientInformation,
            GameOpcode.MapLoaded,
            GameOpcode.Ping,
            GameOpcode.SaveUserOptions
        };

        /// <summary>
        /// Methods the real client sends on its own as it leaves the world, after the server has
        /// already moved it to Loading. They are refused like anything else out of the world, but
        /// they are expected, so they are not reported as a security event.
        ///
        /// RequestVisualCombatMode: tabula_rasa.exe sends it, not the Python.
        /// TRasa::UserControllerStateNormal::LoadTargetProfileData, which loads the camera profile
        /// ("Default", "-zoom"), ends with ClientMovingEntityController::NotifyPythonForLockFacing,
        /// and that calls the manifestation's RequestVisualCombatMode("(b)": whether the profile
        /// locks facing). The profile is reloaded from five of the normal state's virtual methods
        /// - entering the state, switching or reapplying a profile, zoom - so it goes out on every
        /// camera profile change. After PreWonkavate the client leaves the game input state,
        /// exits chat mode and clears the map, which removes its own manifestation and destroys
        /// the user controller. One of those steps reloads the profile, and its report arrives
        /// once the server has set Loading. Dropping it is right: the player is in no cells to
        /// relay it to, and MapChannelManager.RemovePlayer clears the flag it would have set.
        /// </summary>
        private static readonly HashSet<GameOpcode> TransitionStragglers = new()
        {
            GameOpcode.RequestVisualCombatMode
        };

        /// <summary>
        /// Whether this connection may call that method now. There was no such check: every one
        /// of the handlers was reachable in any state, which is what made the stale inventory
        /// lists of a logged-out or mid-zone client worth anything to whoever kept them.
        /// </summary>
        private bool IsExpected(GameOpcode methodId, PythonPacket packet)
        {
            // Nothing at all before the world login: the worldless methods are for the character
            // screen and the loading screen, both of which come after it. They were reachable from
            // a connection that had only done the key exchange, with no account behind it.
            if (!IsAuthenticated())
            {
                ReportOutOfState(methodId);
                return false;
            }

            if (IsInWorld || WorldlessMethods.Contains(methodId))
                return true;

            if (TransitionStragglers.Contains(methodId))
                ReportStraggler(methodId, packet);
            else
                ReportOutOfState(methodId);

            return false;
        }

        private bool _stragglerLogged;
        private long _stragglersSinceLog;
        private long _nextStragglerLogTick;

        /// <summary>
        /// A <see cref="TransitionStragglers"/> method dropped out of the world: a Debug line with
        /// what it carried, rate-limited the same way as <see cref="ReportOutOfState"/>, since
        /// Debug goes to the log file even when it is not shown.
        /// </summary>
        private void ReportStraggler(GameOpcode methodId, PythonPacket packet)
        {
            _stragglersSinceLog++;

            var now = Environment.TickCount64;

            if (_stragglerLogged && now < _nextStragglerLogTick)
                return;

            var call = packet is Packets.MapChannel.Client.RequestVisualCombatModePacket combatMode
                ? $"{methodId}({(combatMode.CombatMode ? "True" : "False")})"
                : methodId.ToString();

            var repeat = _stragglersSinceLog > 1 ? $" ({_stragglersSinceLog} dropped since the last of these)" : "";

            Logger.WriteLog(LogType.Debug, $"{Player?.FamilyName ?? Socket.RemoteAddress.ToString()} sent {call} in state {State}, as the client does while leaving a map; dropped{repeat}.");

            _stragglerLogged = true;
            _stragglersSinceLog = 0;
            _nextStragglerLogTick = now + RefusalLogQuietMs;
        }

        /// <summary>How long this client's refusals stay quiet after one has been logged.</summary>
        private const long RefusalLogQuietMs = 5000;

        private bool _refusalLogged;
        private long _refusalsSinceLog;
        private long _nextRefusalLogTick;

        /// <summary>
        /// The first refusal in full, then at most one every RefusalLogQuietMs saying how many
        /// stood behind it. A client can send these as fast as the wire allows and the log writes
        /// synchronously on the loop thread, so a line each would be the denial of service the
        /// refusal is there to prevent. Refusing costs the packet, not the connection: the real
        /// client has a few of its own to send as it crosses in and out of the world.
        /// </summary>
        private void ReportOutOfState(GameOpcode methodId)
        {
            _refusalsSinceLog++;

            var now = Environment.TickCount64;

            if (_refusalLogged && now < _nextRefusalLogTick)
                return;

            var repeat = _refusalsSinceLog > 1 ? $" ({_refusalsSinceLog} refused since the last of these)" : "";

            Logger.WriteLog(LogType.Security,
                $"Client {Socket.RemoteAddress} sent {methodId} in state {State}; ignored{repeat}.");

            _refusalLogged = true;
            _refusalsSinceLog = 0;
            _nextRefusalLogTick = now + RefusalLogQuietMs;
        }

        /// <summary>
        /// Methods that each send something to every player in range, or queue work on the map,
        /// grouped into shared allowances: (bucket, calls per second, burst). Nothing limited them,
        /// and every other client's send queue is cut off at 512 packets - so one client repeating
        /// a crouch or a gesture as fast as the wire allows filled the queues of everyone around it
        /// and had them disconnected. The rates are well above what a person does by hand.
        /// </summary>
        private static readonly Dictionary<GameOpcode, (string Bucket, double PerSecond, double Burst)> RateLimited = new()
        {
            [GameOpcode.SetDesiredCrouchState] = ("crouch", 4, 8),
            [GameOpcode.RequestGesture] = ("gesture", 2, 5),
            [GameOpcode.RequestGestureWeapon] = ("gesture", 2, 5),
            [GameOpcode.RequestUseObject] = ("use", 4, 8),
            [GameOpcode.RequestVisualCombatMode] = ("stance", 10, 20),
            [GameOpcode.RadialChat] = ("chat", 3, 8),
            [GameOpcode.Shout] = ("chat", 3, 8),
            [GameOpcode.Emote] = ("chat", 3, 8),
            [GameOpcode.PartyChat] = ("chat", 3, 8),
            [GameOpcode.ClanChat] = ("chat", 3, 8),
            [GameOpcode.ClanLeadersChat] = ("chat", 3, 8),
            [GameOpcode.GuildChat] = ("chat", 3, 8),
            [GameOpcode.ChannelChat] = ("chat", 3, 8),
            [GameOpcode.Whisper] = ("chat", 3, 8),
            [GameOpcode.Reply] = ("chat", 3, 8),

            // Not broadcasts, but each one is a synchronous database write on the loop thread.
            [GameOpcode.SaveUserOptions] = ("options", 1, 5),
            [GameOpcode.SaveCharacterOptions] = ("options", 1, 5),
            [GameOpcode.RequestSetAbilitySlot] = ("drawer", 10, 30),
            [GameOpcode.RequestSwapAbilitySlots] = ("drawer", 10, 30),
        };

        private readonly Dictionary<string, (double Tokens, long Tick)> _rateBuckets = new();
        private long _rateDroppedSinceLog;
        private long _nextRateLogTick;

        /// <summary>
        /// A token bucket per group: refills at PerSecond up to Burst, one token a call. A call
        /// with no token is dropped, and the drops are logged at most every RefusalLogQuietMs.
        /// </summary>
        private bool WithinRate(GameOpcode methodId)
        {
            if (!RateLimited.TryGetValue(methodId, out var limit))
                return true;

            var now = Environment.TickCount64;

            var (tokens, tick) = _rateBuckets.TryGetValue(limit.Bucket, out var bucket) ? bucket : (limit.Burst, now);

            tokens = Math.Min(limit.Burst, tokens + (now - tick) * limit.PerSecond / 1000d);

            if (tokens >= 1)
            {
                _rateBuckets[limit.Bucket] = (tokens - 1, now);
                return true;
            }

            _rateBuckets[limit.Bucket] = (tokens, now);
            _rateDroppedSinceLog++;

            if (now >= _nextRateLogTick)
            {
                Logger.WriteLog(LogType.Security,
                    $"{Player?.FamilyName ?? Socket.RemoteAddress.ToString()} is sending {methodId} faster than {limit.PerSecond}/s; "
                    + $"{_rateDroppedSinceLog} call(s) dropped since the last of these.");

                _rateDroppedSinceLog = 0;
                _nextRateLogTick = now + RefusalLogQuietMs;
            }

            return false;
        }

        /// <summary>
        /// World logins a connection may get wrong before it is closed. A refused login used to
        /// leave the connection open to try again, as often as it liked, with an Error line
        /// written synchronously on the loop thread for each. The real client logs in once per
        /// connection, so a few tries is already more than it needs.
        /// </summary>
        private const int MaxFailedLogins = 3;
        private int _failedLogins;

        private void LoginFailed()
        {
            if (++_failedLogins < MaxFailedLogins)
                return;

            Logger.WriteLog(LogType.Security, $"Client {Socket.RemoteAddress} failed {_failedLogins} world logins on one connection; disconnecting.");
            Close();
        }

        private T GetMessageAs<T>(ProtocolPacket protocolPacket)
            where T : class, IClientMessage
        {
            if (protocolPacket.Message is T message)
            {
                return message;
            }
            throw new InvalidClientMessageException();
        }

        public bool IsAuthenticated()
        {
            return State != ClientState.Connected && State != ClientState.Disconnected;
        }

        #region Socketing
        private void OnEncrypt(BufferData data, ref int length)
        {
            // The frame body is one byte of padding count, that many bytes of padding (the
            // count byte itself is the first of them), then the packet - so the packet moves
            // right by the count and the cipher runs over the lot. This used to be done through
            // a second pool buffer per send, which doubled what every send took from a pool the
            // whole server shares, and dereferenced the null it gets when that pool is empty.
            var paddingCount = (byte) (8 - length % 8);
            var start = data.BaseOffset + data.Offset;

            if (data.Offset + length + paddingCount > data.MaxLength)
                throw new InvalidOperationException($"A {length} byte packet leaves no room for its {paddingCount} bytes of padding.");

            Array.Copy(data.Buffer, start, data.Buffer, start + paddingCount, length);
            Array.Clear(data.Buffer, start, paddingCount);
            data.Buffer[start] = paddingCount;

            length += paddingCount;

            GameCryptManager.Encrypt(data.Buffer, start, ref length, length, Data);
        }

        private bool OnDecrypt(BufferData data)
        {
            var result = GameCryptManager.Decrypt(data.Buffer, data.BaseOffset + data.Offset, data.RemainingLength, Data);
            if (!result)
                return false;

            var blowfishPadding = data[data.Offset] & 0xF;
            if (blowfishPadding > 8)
                throw new Exception("More than 8 bytes of blowfish padding was added to the packet?");

            data.Offset += blowfishPadding;

            return true;
        }

        private void OnError(SocketAsyncEventArgs args)
        {
            Close(false);
        }

        /// <summary>
        /// The socket has given up on this connection - a full send queue, a stream that stopped
        /// framing, or no buffers left to serve it. Close without trying to send anything: either
        /// nothing can reach them, or nothing they send can be read.
        /// </summary>
        private void OnDrop(string reason)
        {
            Close(false);
        }
		
        private void OnReceive(BufferData data)
        {
            // IOCP thread. Do not touch _incomingDataQueue here - see the field comment.
            var count = data.RemainingLength;
            if (count <= 0 || State == ClientState.Disconnected)
                return;

            // Same rent-and-copy CopyFromArray() used to do; only the List.Add is deferred.
            var chunk = ArrayPool<byte>.Shared.Rent(count);
            Buffer.BlockCopy(data.Buffer, data.BaseOffset + data.Offset, chunk, 0, count);

            var pending = Interlocked.Add(ref _pendingBytes, count);
            if (pending > MaxPendingBytes)
            {
                ArrayPool<byte>.Shared.Return(chunk);
                Logger.WriteLog(LogType.Security, $"Client {Socket.RemoteAddress} has {pending} bytes of undrained input (limit {MaxPendingBytes}), disconnecting.");
                Close(false);
                return;
            }

            _pendingChunks.Enqueue((chunk, count));
        }

        // MainLoop thread. Moves everything the socket thread has handed off into the
        // stream. AddSharedPoolArray takes ownership of the pooled array; RemoveBytes
        // returns it to the pool once it has been consumed, exactly as before.
        private void DrainPendingChunks()
        {
            while (_pendingChunks.TryDequeue(out var chunk))
            {
                Interlocked.Add(ref _pendingBytes, -chunk.Length);
                _incomingDataQueue.AddSharedPoolArray(chunk.Buffer, chunk.Length);
            }
        }

        // Called from Close() after State is Disconnected, so OnReceive() no longer
        // enqueues. A completion already past that check can still slip one chunk in
        // afterwards; that array is simply collected by the GC, which is harmless.
        private void DiscardPendingChunks()
        {
            while (_pendingChunks.TryDequeue(out var chunk))
            {
                Interlocked.Add(ref _pendingBytes, -chunk.Length);
                ArrayPool<byte>.Shared.Return(chunk.Buffer);
            }
        }

        /// <summary>
        /// Hands the inbound stream's pooled arrays back. Anything drained into it but not yet
        /// decoded - a partial frame, which is what every Alt+F4 leaves behind - is held by
        /// arrays only Dispose returns, so the shared pool lost a block on each of those
        /// disconnects. DiscardPendingChunks covers the other half, the chunks not yet drained.
        ///
        /// Called by the MainLoop when it finally drops the client, not by Close(): Close() runs
        /// on socket threads too, and this stream belongs to the MainLoop. Disposing it from
        /// under a tick that is mid-decode is the same class of bug as the one the hand-off
        /// queue exists to prevent.
        /// </summary>
        internal void ReleaseInboundBuffers()
        {
            _incomingDataQueue.Dispose();
        }

        private IEnumerable<ProtocolPacket> DecodeIncomingPackets()
        {
            // A skipped packet (out of order, or the untyped send-timeout check) used to come
            // back as null too, which read as "no more data" and ended the loop for the tick;
            // everything queued behind it waited for the next one, 100 ms later, and a stream
            // with a skipped packet in every tick fell further behind on each. Skips now
            // continue and only an incomplete frame stops.
            while (TryDecodeNextPacket(out var packet))
                if (packet != null)
                    yield return packet;
        }

        /// <returns>
        /// false when the queue holds no complete frame; true otherwise, with the packet, or
        /// null for a frame that was consumed and dropped.
        /// </returns>
        private bool TryDecodeNextPacket(out ProtocolPacket packet)
        {
            packet = null;

            // If there is not enough data to read the packet size at all, then stop processing
            if (_incomingDataQueue.Length < 2)
                return false;

            using var br = new BinaryReader(_incomingDataQueue, Encoding.UTF8, true);

            // Peek the packet size to determine if the whole packet has arrived
            var startPosition = _incomingDataQueue.Position;

            // Read the size of the next packet
            var packetSize = br.ReadUInt16();

            // Rewind the stream to the starting position
            _incomingDataQueue.Position = startPosition;

            // If the packet is fragmented and not all the fragments has arrived yet, then stop processing
            if (packetSize > _incomingDataQueue.Length)
                return false;

            // Construct and the packet
            var rawPacket = new ProtocolPacket();

            rawPacket.Read(br);

            // Check for overreading or underreading the packet
            if (_incomingDataQueue.Position != startPosition + packetSize)
                throw new Exception($"ProtocolPacket over or under read! Start position: {startPosition} | Packet size: {packetSize} | End position: {_incomingDataQueue.Position}!");

            // Advance the stream by removing the already processed data
            _incomingDataQueue.RemoveBytes(packetSize);

            // Throw away any packet that came out of order, if it came on a channel
            if (rawPacket.Channel != 0)
            {
                // If an out of sequence packet arrived, then throw it away
                if (rawPacket.SequenceNumber < ReceiveSequence[rawPacket.Channel])
                {
                    // Movement arrives on a sequenced channel, so this is reachable in normal
                    // play. Dropping the stale packet is correct; breaking into a debugger on
                    // a headless server is not.
                    Logger.WriteLog(LogType.Debug, $"Dropped out-of-order packet on channel {rawPacket.Channel} (seq {rawPacket.SequenceNumber} < {ReceiveSequence[rawPacket.Channel]}) from {Socket.RemoteAddress}.");

                    return true;
                }

                // AddOrUpdate the receive sequence for the channel
                ReceiveSequence[rawPacket.Channel] = rawPacket.SequenceNumber;
            }

            // Some internal send timeout check, skip the packet
            if (rawPacket.Type == ClientMessageOpcode.None)
            {
                if (rawPacket.Size != 4)
                    Logger.WriteLog(LogType.Debug, $"Skipped an untyped packet of size {rawPacket.Size} (expected the 4-byte send-timeout check) from {Socket.RemoteAddress}.");

                return true;
            }

            packet = rawPacket;

            return true;
        }
        #endregion

        public void SaveCharacter()
        {
            var player = Player;

            // Player is field-initialized to an empty Manifestation, so a null check alone
            // never fires. A client that disconnects before entering the world (character
            // selection, failed login, idle timeout) still has Id == 0, and looking that up
            // throws EntityNotFoundException.
            if (player == null || player.Id == 0)
            {
                return;
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.Characters.SaveCharacter(player);
            unitOfWork.Complete();
        }

        public void ReloadGameAccountEntry()
        {
            if (AccountEntry == null)
            {
                throw new InvalidOperationException("Client must be initialized by handling a login packet first.");
            }

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            LoadGameAccountEntry(unitOfWork, AccountEntry.Id);
        }

        private void LoadGameAccountEntry(ICharUnitOfWork unitOfWork, uint id)
        {
            AccountEntry = unitOfWork.GameAccounts.Get(id);
        }
    }
}
