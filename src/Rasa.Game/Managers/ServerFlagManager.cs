using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Game.Server;

    /// <summary>
    /// The server-wide flag set, and telling clients about it.
    ///
    /// The client keeps its own copy in client/serverflagmanager.py and consults it before
    /// offering a feature - minion commands are the only reader left in the shipped scripts. It
    /// is told the whole set when it enters the world, and one flag at a time as they change.
    /// </summary>
    public class ServerFlagManager
    {
        private static ServerFlagManager _instance;
        private static readonly object InstanceLock = new object();

        public static ServerFlagManager Instance
        {
            get
            {
                if (_instance == null)
                    lock (InstanceLock)
                        _instance ??= new ServerFlagManager();

                return _instance;
            }
        }

        private ServerFlagManager()
        {
        }

        /// <summary>
        /// The flags that are on. Read on socket threads and written from the console thread, so
        /// every reader takes a copy under the lock rather than walking the live set.
        /// </summary>
        private readonly HashSet<ServerFlag> _flags = new HashSet<ServerFlag>();
        private readonly object _flagLock = new object();

        public List<ServerFlag> Flags
        {
            get { lock (_flagLock) return _flags.OrderBy(f => (uint)f).ToList(); }
        }

        public bool IsSet(ServerFlag flag)
        {
            lock (_flagLock) return _flags.Contains(flag);
        }

        /// <summary>
        /// Reads the starting set from GameDataConfig.ServerFlags, by name or by number. Names
        /// are matched against this enum and against the client's own spelling, so
        /// "MINION_COMMANDS" out of the client's serverflags.py works as well as "MinionCommands".
        /// </summary>
        public void LoadConfiguredFlags(string[] configured)
        {
            lock (_flagLock)
            {
                _flags.Clear();

                if (configured == null)
                    return;

                foreach (var entry in configured)
                {
                    if (TryParse(entry, out var flag))
                        _flags.Add(flag);
                    else
                        Logger.WriteLog(LogType.Error,
                            $"ServerFlags: '{entry}' is not a server flag. Known flags: {KnownFlags()}");
                }

                if (_flags.Count > 0)
                    Logger.WriteLog(LogType.Initialize,
                        $"Server flags set: {string.Join(", ", _flags.OrderBy(f => (uint)f))}");
            }
        }

        /// <summary>Tells one client the whole set, as it enters the world.</summary>
        public void SendFlags(Client client)
        {
            client.CallMethod(SysEntity.ClientServerFlagManagerId, new ServerFlagsPacket(Flags));
        }

        /// <summary>
        /// Turns a flag on for everyone now and for everyone who logs in afterwards. Returns
        /// false if it was already on, so a caller can say so.
        /// </summary>
        public bool Set(ServerFlag flag)
        {
            lock (_flagLock)
                if (!_flags.Add(flag))
                    return false;

            foreach (var client in InWorld())
                client.CallMethod(SysEntity.ClientServerFlagManagerId, new SetServerFlagPacket(flag));

            Logger.WriteLog(LogType.Command, $"Server flag {flag} ({(uint)flag}) is now set.");
            return true;
        }

        /// <summary>
        /// Turns a flag off.
        ///
        /// ClearServerFlag goes out because that is the message for this, and then the whole set
        /// goes out behind it because that is the message the client can act on: its
        /// Recv_ClearServerFlag calls list.pop with two arguments and raises before it removes
        /// anything, while Recv_ServerFlags replaces the list outright.
        /// </summary>
        public bool Clear(ServerFlag flag)
        {
            lock (_flagLock)
                if (!_flags.Remove(flag))
                    return false;

            foreach (var client in InWorld())
            {
                client.CallMethod(SysEntity.ClientServerFlagManagerId, new ClearServerFlagPacket(flag));
                SendFlags(client);
            }

            Logger.WriteLog(LogType.Command, $"Server flag {flag} ({(uint)flag}) is now clear.");
            return true;
        }

        /// <summary>
        /// Clients far enough along to have a flag manager listening. Copied out of Server.Clients
        /// under its lock, because sending walks the list and a send can disconnect a client.
        /// </summary>
        private static List<Client> InWorld()
        {
            lock (Server.Clients)
                return Server.Clients.Where(c => c != null && c.State == ClientState.Ingame).ToList();
        }

        public static bool TryParse(string entry, out ServerFlag flag)
        {
            flag = default;

            if (string.IsNullOrWhiteSpace(entry))
                return false;

            if (uint.TryParse(entry, out var id))
            {
                if (!Enum.IsDefined(typeof(ServerFlag), id))
                    return false;

                flag = (ServerFlag)id;
                return true;
            }

            // The client writes them as MINION_COMMANDS; this enum writes MinionCommands. Drop
            // the underscores and compare without case and both spellings answer to each other.
            var wanted = entry.Replace("_", string.Empty);

            foreach (var known in Enum.GetValues<ServerFlag>())
                if (string.Equals(known.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    flag = known;
                    return true;
                }

            return false;
        }

        public static string KnownFlags()
        {
            return string.Join(", ", Enum.GetValues<ServerFlag>().Select(f => $"{f} ({(uint)f})"));
        }
    }
}
