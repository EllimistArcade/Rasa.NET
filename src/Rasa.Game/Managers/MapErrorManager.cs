using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Game.Server;

    /// <summary>
    /// What is wrong with the world's data, kept per map so a GM standing in a broken one can be
    /// told about it.
    ///
    /// The original server sent the tracebacks its map scripts raised. This one has no map
    /// scripts, but it has the same class of problem and already writes it to the error log and
    /// carries on: a spawn pool naming a creature that is not in the database, a creature whose
    /// entity class was never given the Creature augmentation, a teleporter of a type nothing
    /// handles. Each of those leaves part of a map not working, which is exactly what the
    /// client's dialog is for, and until now the only way to find out was to read the log.
    /// </summary>
    public class MapErrorManager
    {
        private static MapErrorManager _instance;
        private static readonly object InstanceLock = new object();

        public static MapErrorManager Instance
        {
            get
            {
                if (_instance == null)
                    lock (InstanceLock)
                        _instance ??= new MapErrorManager();

                return _instance;
            }
        }

        private MapErrorManager()
        {
        }

        /// <summary>Errors that belong to the world rather than to one map. Every map shows these too.</summary>
        public const uint ServerWide = 0;

        /// <summary>
        /// The dialog is one message box on a 1024-wide screen and the player has to read it.
        /// Past this many the rest are in the log, where they were going anyway.
        /// </summary>
        private const int MaxPerMap = 20;

        private const int MaxMessageLength = 300;
        private const string Truncated = "(more errors for this map in the server log)";

        private readonly Dictionary<uint, List<string>> _errors = new Dictionary<uint, List<string>>();
        private readonly object _errorLock = new object();

        /// <summary>
        /// Notes something wrong with a map's data. Safe to call from the spawn path, which
        /// retries a broken spawn pool every respawn: the same message is only kept once.
        /// </summary>
        public void Record(uint mapContextId, string message)
        {
            // An entry the client cannot show is worse than no entry: its dialog does
            // error.splitlines().pop(), and an empty string has no last line to pop.
            if (string.IsNullOrWhiteSpace(message))
                return;

            var line = Flatten(message);

            lock (_errorLock)
            {
                if (!_errors.TryGetValue(mapContextId, out var list))
                {
                    list = new List<string>();
                    _errors.Add(mapContextId, list);
                }

                if (list.Contains(line))
                    return;

                if (list.Count >= MaxPerMap)
                    return;

                if (list.Count == MaxPerMap - 1)
                {
                    list.Add(Truncated);
                    return;
                }

                list.Add(line);
            }
        }

        /// <summary>Notes something wrong with the world's data rather than one map's.</summary>
        public void Record(string message)
        {
            Record(ServerWide, message);
        }

        /// <summary>What a player in this map should be shown: the world's errors, then the map's.</summary>
        public List<string> Errors(uint mapContextId)
        {
            lock (_errorLock)
            {
                var result = new List<string>();

                if (_errors.TryGetValue(ServerWide, out var world))
                    result.AddRange(world);

                if (mapContextId != ServerWide && _errors.TryGetValue(mapContextId, out var map))
                    result.AddRange(map);

                return result;
            }
        }

        /// <summary>Just this bucket, without the world's errors folded in.</summary>
        public List<string> ErrorsFor(uint mapContextId)
        {
            lock (_errorLock)
                return _errors.TryGetValue(mapContextId, out var list) ? new List<string>(list) : new List<string>();
        }

        /// <summary>Every map that has anything against it, for the console.</summary>
        public List<(uint MapContextId, int Count)> Summary()
        {
            lock (_errorLock)
                return _errors.Select(e => (e.Key, e.Value.Count)).OrderBy(e => e.Key).ToList();
        }

        public void Clear()
        {
            lock (_errorLock)
                _errors.Clear();
        }

        /// <summary>
        /// Puts the dialog in front of one client, if there is anything to say. Returns false when
        /// there was nothing, so a command can say so rather than leaving the GM wondering.
        /// </summary>
        public bool SendTo(Client client)
        {
            if (client?.Player == null)
                return false;

            var errors = Errors(client.Player.MapContextId);

            if (errors.Count == 0)
                return false;

            client.CallMethod(SysEntity.ClientMethodId, new DisplayMapErrorsPacket(errors));
            return true;
        }

        /// <summary>
        /// One line, short enough to read. The dialog only shows the last line of an entry and the
        /// log takes the whole thing, so a message that is already one line loses nothing either
        /// way - and a multi-line one would show only its tail in the dialog.
        /// </summary>
        private static string Flatten(string message)
        {
            var line = string.Join(" / ", message.Split('\n', '\r')
                                                 .Select(part => part.Trim())
                                                 .Where(part => part.Length > 0));

            return line.Length > MaxMessageLength
                ? line.Substring(0, MaxMessageLength - 3) + "..."
                : line;
        }
    }
}
