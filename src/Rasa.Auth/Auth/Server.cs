using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Hosting;

namespace Rasa.Auth
{
    using Commands;
    using Config;
    using Data;
    using Hosting;
    using Memory;
    using Networking;
    using Packets.Communicator;
    using Packets.Auth.Server;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Auth;
    using Threading;
    using Timer;

    public class Server : ILoopable, IRasaServer
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IAuthUnitOfWorkFactory _authUnitOfWorkFactory;

        public string ServerType { get; } = "Authentication";

        public const int MainLoopTime = 100; // Milliseconds

        public Config Config { get; private set; }
        public LengthedSocket AuthCommunicator { get; private set; }
        public LengthedSocket ListenerSocket { get; private set; }
        public List<Client> Clients { get; } = new List<Client>();
       
        public List<ServerInfo> ServerList { get; } = new List<ServerInfo>();
        public MainLoop Loop { get; }
        public Timer Timer { get; }
        public bool Running => Loop != null && Loop.Running;

        private readonly List<Client> _clientsToRemove = new List<Client>();
        private List<CommunicatorClient> GameServerQueue { get; } = new List<CommunicatorClient>();
        private Dictionary<byte, CommunicatorClient> GameServers { get; } = new Dictionary<byte, CommunicatorClient>();

        public Server(IHostApplicationLifetime hostApplicationLifetime, IAuthUnitOfWorkFactory authUnitOfWorkFactory)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _authUnitOfWorkFactory = authUnitOfWorkFactory;

            Configuration.OnLoad += ConfigLoaded;
            Configuration.OnReLoad += ConfigReLoaded;
            Configuration.Load();

            Loop = new MainLoop(this, MainLoopTime);
            Timer = new Timer();

            SetupServerList();

            LengthedSocket.InitializeEventArgsPool(Config.SocketAsyncConfig.MaxClients * Config.SocketAsyncConfig.ConcurrentOperationsByClient);

            BufferManager.Initialize(Config.SocketAsyncConfig.BufferSize, Config.SocketAsyncConfig.MaxClients, Config.SocketAsyncConfig.ConcurrentOperationsByClient);
            
            CommandProcessor.RegisterCommand("exit", ProcessExitCommand);
            CommandProcessor.RegisterCommand("reload", ProcessReloadCommand);
            CommandProcessor.RegisterCommand("create", ProcessCreateCommand);
            CommandProcessor.RegisterCommand("ban", parts => ProcessLockCommand(parts, true));
            CommandProcessor.RegisterCommand("unban", parts => ProcessLockCommand(parts, false));
        }

        ~Server()
        {
            Shutdown();
        }

        #region Configuration
        private static void ConfigReLoaded()
        {
            Logger.WriteLog(LogType.Initialize, "Config file reloaded by external change!");

            // Totally reload the configuration, because it's automatic reload case can only handle one reload. Our code's bug?
            Configuration.Load();
        }

        private void ConfigLoaded()
        {
            var oldConfig = Config;

            Config = new Config();
            Configuration.Bind(Config);

            Logger.UpdateConfig(Config.LoggerConfig);

            // Handle reloading the config and updating the list visibility
            if (oldConfig == null || oldConfig.AuthListType == Config.AuthListType)
                return;

            lock (ServerList)
            {
                ServerList.Clear();
                SetupServerList();
                GenerateServerList();
            }
        }
        #endregion

        public void Disconnect(Client client)
        {
            lock (_clientsToRemove)
                _clientsToRemove.Add(client);
        }

        private void SetupServerList()
        {
            if (Config.AuthListType != AuthListType.All)
                return;

            foreach (var s in Config.Servers)
            {
                if (!byte.TryParse(s.Key, out byte id))
                    continue;

                ServerList.Add(new ServerInfo
                {
                    AgeLimit = 0,
                    CurrentPlayers = 0,
                    GamePort = 0,
                    Ip = IPAddress.None,
                    MaxPlayers = 0,
                    PKFlag = 0,
                    QueuePort = 0,
                    ServerId = id,
                    Status = 0
                });
            }
        }

        #region Socketing
        public bool Start()
        {
            // If no config file has been found, these values are 0 by default
            if (Config.AuthConfig.Port == 0 || Config.AuthConfig.Backlog == 0)
            {
                Logger.WriteLog(LogType.Error, "Invalid config values!");
                return false;
            }

            try
            {
                ListenerSocket = new LengthedSocket(SizeType.Word);
                ListenerSocket.OnError += OnError;
                ListenerSocket.OnAccept += OnAccept;
                ListenerSocket.Bind(new IPEndPoint(IPAddress.Any, Config.AuthConfig.Port));
                ListenerSocket.Listen(Config.AuthConfig.Backlog);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Unable to create or start listening on the client socket! Exception:");
                Logger.WriteLog(LogType.Error, e);

                return false;
            }

            Loop.Start();

            if (!SetupCommunicator())
                return false;

            Logger.WriteLog(LogType.Network, "*** Listening for clients on port {0}", Config.AuthConfig.Port);

            ListenerSocket.AcceptAsync();

            // TODO: Set up timed events (query stuff, internal communication, etc...)

            return true;
        }

        private static void OnError(SocketAsyncEventArgs args)
        {
            if (args.LastOperation == SocketAsyncOperation.Accept && args.AcceptSocket != null &&
                args.AcceptSocket.Connected)
                args.AcceptSocket.Shutdown(SocketShutdown.Both);
        }

        private void OnAccept(LengthedSocket newSocket)
        {
            ListenerSocket.AcceptAsync();

            if (newSocket == null)
                return;

            lock (Clients)
                Clients.Add(new Client(newSocket, this, _authUnitOfWorkFactory));
        }
        #endregion

        #region Communicator
        private bool SetupCommunicator()
        {
            if (Config.CommunicatorConfig.Port == 0 || Config.CommunicatorConfig.Address == null || Config.CommunicatorConfig.Backlog == 0)
            {
                Logger.WriteLog(LogType.Error, "Invalid Communicator config data! Can't connect!");
                return false;
            }

            try
            {
                AuthCommunicator = new LengthedSocket(SizeType.Word);
                AuthCommunicator.OnAccept += OnCommunicatorAccept;
                AuthCommunicator.Bind(new IPEndPoint(IPAddress.Parse(Config.CommunicatorConfig.Address), Config.CommunicatorConfig.Port));
                AuthCommunicator.Listen(Config.CommunicatorConfig.Backlog);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, "Unable to create or start listening on the communicator socket! Exception:");
                Logger.WriteLog(LogType.Error, e);

                return false;
            }

            AuthCommunicator.AcceptAsync();

            Timer.Add("ServerInfoUpdate", 1000, true, () =>
            {
                lock (GameServers)
                    foreach (var server in GameServers)
                        if ((DateTime.Now - server.Value.LastRequestTime).TotalMilliseconds > 30000)
                            server.Value.RequestServerInfo();
            });

            Logger.WriteLog(LogType.Network, $"*** Listening for Game servers on port {Config.CommunicatorConfig.Port}");

            return true;
        }

        private void OnCommunicatorAccept(LengthedSocket socket)
        {
            AuthCommunicator.AcceptAsync();

            lock (GameServers)
                GameServerQueue.Add(new CommunicatorClient(socket, this));

            Logger.WriteLog(LogType.Network, $"A Game server has connected! Remote: {socket.RemoteAddress}");
        }

        public bool AuthenticateGameServer(LoginRequestPacket packet, CommunicatorClient client)
        {
            // Lock order everywhere in this class is Clients -> ServerList -> GameServers, so
            // nothing that holds GameServers may go on to regenerate the server list.
            // DisconnectCommunicator does exactly that, which is why the rejections are decided
            // under the lock and carried out after it.
            string rejection;
            LogType rejectionLogType;

            lock (GameServers)
            {
                if (GameServers.ContainsKey(packet.ServerId))
                {
                    rejection = "A server tried to connect to an already in use server slot!";
                    rejectionLogType = LogType.Debug;
                }
                else if (!Config.Servers.ContainsKey(packet.ServerId.ToString()))
                {
                    rejection = "A server tried to connect to a non-defined server slot!";
                    rejectionLogType = LogType.Debug;
                }
                else if (Config.Servers[packet.ServerId.ToString()] != packet.Password)
                {
                    rejection = "A server tried to log in with an invalid password!";
                    rejectionLogType = LogType.Error;
                }
                else
                {
                    GameServerQueue.Remove(client);
                    GameServers.Add(packet.ServerId, client);

                    Logger.WriteLog(LogType.Network, $"The Game server (Id: {packet.ServerId}, Address: {client.Socket.RemoteAddress}, Public Address: {packet.PublicAddress}) has authenticated! Requesting info...");

                    return true;
                }
            }

            DisconnectCommunicator(client);
            Logger.WriteLog(rejectionLogType, $"{rejection} Remote Address: {client.Socket.RemoteAddress}");

            return false;
        }

        public void UpdateServerInfo(CommunicatorClient client, ServerInfoResponsePacket packet)
        {
            GenerateServerList();
            BroadcastServerList();
        }

        public void RedirectResponse(CommunicatorClient client, RedirectResponsePacket packet)
        {
            Client authClient;
            lock (Clients)
                // AccountEntry is null on every connection still at the login screen, and this
                // runs on the communicator thread whenever a game server answers a redirect.
                authClient = Clients.FirstOrDefault(c => c.AccountEntry != null && c.AccountEntry.Id == packet.AccountId);

            ServerInfo info;
            lock (ServerList)
                info = ServerList.FirstOrDefault(i => i.ServerId == client.ServerId);

            if (authClient != null && info != null)
                authClient.RedirectionResult(packet.Response, info);
        }

        public void RequestRedirection(Client client, byte serverId)
        {
            lock (GameServers)
                if (GameServers.ContainsKey(serverId))
                    GameServers[serverId].RequestRedirection(client);
        }

        public void DisconnectCommunicator(CommunicatorClient client)
        {
            if (client == null)
                return;

            lock (GameServers)
            {
                GameServerQueue.Remove(client);

                if (client.ServerId != 0)
                    GameServers.Remove(client.ServerId);
            }

            // Outside the GameServers lock: GenerateServerList takes ServerList and then
            // GameServers, and calling it from inside GameServers inverted that order against
            // UpdateServerInfo, so one game server dropping while another reported its info
            // could deadlock both communicator threads for good.
            GenerateServerList();

            Timer.Add($"Disconnect-comm-{DateTime.Now.Ticks}", 1000, false, () =>
            {
                client.Socket?.Close();
            });

            Logger.WriteLog(LogType.Network, $"The game server (Id: {client.ServerId}, Address: {client.Socket.RemoteAddress}) has disconnected!");
        }

        private void GenerateServerList()
        {
            lock (ServerList)
            {
                var toRemove = new List<ServerInfo>();

                lock (GameServers)
                {
                    foreach (var sInfo in ServerList)
                    {
                        if (GameServers.TryGetValue(sInfo.ServerId, out CommunicatorClient client))
                        {
                            sInfo.Setup(client.PublicAddress, client.QueuePort, client.GamePort, client.AgeLimit, client.PKFlag, client.CurrentPlayers, client.MaxPlayers);
                            continue;
                        }

                        if (Config.AuthListType == AuthListType.Online)
                        {
                            toRemove.Add(sInfo);
                            continue;
                        }

                        sInfo.Clear();
                    }

                    foreach (var server in GameServers)
                    {
                        if (ServerList.All(s => s.ServerId != server.Key))
                        {
                            ServerList.Add(new ServerInfo
                            {
                                AgeLimit = server.Value.AgeLimit,
                                PKFlag = server.Value.PKFlag,
                                CurrentPlayers = server.Value.CurrentPlayers,
                                MaxPlayers = server.Value.MaxPlayers,
                                QueuePort = server.Value.QueuePort,
                                GamePort = server.Value.GamePort,
                                Ip = server.Value.PublicAddress,
                                ServerId = server.Key,
                                Status = 1
                            });
                        }
                    }
                }

                if (toRemove.Count == 0)
                    return;

                foreach (var rem in toRemove)
                    ServerList.Remove(rem);
            }
        }
        #endregion

        public void Shutdown()
        {
            ListenerSocket?.Close();
            ListenerSocket = null;

            Loop.Stop();
        }

        public void MainLoop(long delta)
        {
            // Same rule as the game server: this is the only thread, and a fault in one
            // connection must not cost the tick for the connections after it in the list.
            try
            {
                Timer.Update(delta);
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Error updating the auth server timers: {e}");
            }

            if (Clients.Count == 0)
                return;

            lock (Clients)
            {
                foreach (var c in Clients)
                {
                    try
                    {
                        c.Update(delta);
                    }
                    catch (Exception e)
                    {
                        Logger.WriteLog(LogType.Error, $"Error updating auth client {c.Socket?.RemoteAddress}, disconnecting it: {e}");

                        try
                        {
                            c.Close();
                        }
                        catch (Exception inner)
                        {
                            Logger.WriteLog(LogType.Error, $"And closing it threw as well: {inner}");
                        }
                    }
                }

                if (_clientsToRemove.Count > 0)
                {
                    lock (_clientsToRemove)
                    {
                        foreach (var client in _clientsToRemove)
                            Clients.Remove(client);

                        _clientsToRemove.Clear();
                    }
                }
            }
        }

        public void BroadcastServerList()
        {
            var servers = GetServerListSnapshot();

            lock (Clients)
                foreach (var c in Clients)
                    if (c.State == ClientState.ServerList)
                        c.SendPacket(new SendServerListExtPacket(servers, c.AccountEntry.LastServerId));
        }

        /// <summary>
        /// A copy of the server list to serialize from. Send writes the packet on the calling
        /// thread, and the list is rebuilt on the communicator threads, so handing the live
        /// list to a packet let the main loop enumerate it while GenerateServerList changed it.
        /// </summary>
        public List<ServerInfo> GetServerListSnapshot()
        {
            lock (ServerList)
                return ServerList.Select(s => s.Copy()).ToList();
        }

        #region Commands
        private void ProcessExitCommand(string[] parts)
        {
            var minutes = 0;

            if (parts.Length > 1)
                minutes = int.Parse(parts[1]);

            Timer.Add("exit", minutes * 60000, false, () =>
            {
                Shutdown();
                _hostApplicationLifetime.StopApplication();
            });

            Logger.WriteLog(LogType.Command, $"Exiting the server in {minutes} minute(s).");
        }

        private static void ProcessReloadCommand(string[] parts)
        {
            if (parts.Length > 1 && parts[1] == "config")
            {
                Configuration.Load();
                return;
            }

            Logger.WriteLog(LogType.Command, "Invalid reload command!");
        }

        /// <summary>
        /// ban &lt;username&gt; / unban &lt;username&gt;. Sets account.locked, which GetByUserName
        /// already refuses at login, then deals with anyone already past that check: the player's
        /// auth connection (server list screen) is closed here, and every connected game server is
        /// told so it can kick them from the queue or world and refuse a pending handoff.
        /// </summary>
        private void ProcessLockCommand(string[] parts, bool locked)
        {
            var command = locked ? "ban" : "unban";

            if (parts.Length < 2)
            {
                Logger.WriteLog(LogType.Command, $"Invalid {command} command! Usage: {command} <username>");
                return;
            }

            AuthAccountEntry account;

            try
            {
                using var unitOfWork = _authUnitOfWorkFactory.Create();
                account = unitOfWork.AuthAccountRepository.SetLocked(parts[1], locked);
                unitOfWork.Complete();
            }
            catch (Exception e)
            {
                Logger.WriteLog(LogType.Error, $"Could not {command} {parts[1]}: {e.Message}");
                return;
            }

            if (account == null)
            {
                Logger.WriteLog(LogType.Command, $"No account with username {parts[1]}.");
                return;
            }

            if (locked)
            {
                List<Client> connected;

                // AccountEntry is null until a client has logged in.
                lock (Clients)
                    connected = Clients.Where(c => c.AccountEntry != null && c.AccountEntry.Id == account.Id).ToList();

                foreach (var client in connected)
                    client.Close();
            }

            var notice = new AccountLockChangedPacket { AccountId = account.Id, Locked = locked };
            var notified = 0;

            lock (GameServers)
                foreach (var server in GameServers.Values)
                {
                    if (!server.Connected)
                        continue;

                    server.Socket.Send(notice);
                    notified++;
                }

            Logger.WriteLog(LogType.Command, $"{(locked ? "Banned" : "Unbanned")} account {account.Username} ({account.Id}); notified {notified} game server(s).");
        }

        private void ProcessCreateCommand(string[] parts)
        {
            if (parts.Length < 4)
            {
                Logger.WriteLog(LogType.Command, "Invalid create account command! Usage: create <email> <username> <password>");
                return;
            }

            var email = parts[1];
            var userName = parts[2];
            var password = parts[3];

            try
            {
                using var unitOfWork = _authUnitOfWorkFactory.Create();
                unitOfWork.AuthAccountRepository.Create(email, userName, password);
                unitOfWork.Complete();

                Logger.WriteLog(LogType.Command, $"Created account: {parts[2]}! (Password: {parts[3]})");
            }
            catch
            {
                Logger.WriteLog(LogType.Error, "Username or email is already taken!");
            }
        }

        /*private void ProcessRestartCommand(string[] parts)
        {
            // TODO: delayed restart, with contacting globals, so they can warn players not to leave the server, or they won't be able to reconnect
        }

        private void ProcessShutdownCommand(string[] parts)
        {
            // TODO: delayed shutdown, with contacting globals, so they can warn players not to leave the server, or they won't be able to reconnect
            // TODO: add timer to report the remaining time until shutdown?
            // TODO: add timer to contact global servers to tell them periodically that we're getting shut down?
        }*/
        #endregion
    }
}
