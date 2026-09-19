using System;
using System.Collections.Generic;

namespace Rasa.Timer
{
    public class Timer
    {
        private readonly Dictionary<string, TimedItem> _timedItems = new();

        public void Add(string name, long timer, bool repeating, Action action)
        {
            lock (_timedItems)
            {
                if (_timedItems.ContainsKey(name))
                    _timedItems.Remove(name);

                _timedItems.Add(name, new TimedItem(name, timer, repeating, action));
            }
        }

        public void Remove(string name)
        {
            lock (_timedItems)
            {
                if (_timedItems.ContainsKey(name))
                    _timedItems.Remove(name);
            }
        }

        /// <summary>
        /// Fires whatever is due. Driven by a server's main loop, so nothing here may throw:
        /// the loop thread has no handler above it and an escaping exception ends the process.
        ///
        /// Two ways that used to happen, both fixed here rather than in the callbacks:
        ///
        /// A callback threw, and it came straight back out through the main loop. Each one is
        /// now caught and logged against the timer's name, and the rest of the pass still runs -
        /// one broken timer must not stop the others from firing.
        ///
        /// A callback added or removed a timer, which invalidated the enumerator and threw
        /// "Collection was modified" on the next step - from the loop, not from the callback,
        /// so catching around the call would not have covered it. This lock is reentrant, so a
        /// callback reaching Add or Remove on the same thread walks straight into the dictionary
        /// being walked. Game.Server does exactly that: the CommReconnect callback calls
        /// ConnectCommunicator, and LengthedSocket.ConnectAsync runs the completion inline when
        /// the connect finishes synchronously - which a refused connection does - so the error
        /// handler reaches Timer.Add while this is still iterating. A game server running with
        /// the auth server down could take itself out that way. Iterating a snapshot removes
        /// the hazard for every caller instead of asking each not to touch the timer.
        /// </summary>
        public void Update(long delta)
        {
            lock (_timedItems)
            {
                List<string> toRemove = null;

                foreach (var item in new List<KeyValuePair<string, TimedItem>>(_timedItems))
                {
                    if (!item.Value.Update(delta))
                        continue;

                    // Before the callback: a non-repeating timer is spent once it has fired,
                    // and one that throws would otherwise be left behind to throw again on
                    // every tick from here on.
                    if (!item.Value.Repeating)
                    {
                        if (toRemove == null)
                            toRemove = new();

                        toRemove.Add(item.Key);
                    }

                    try
                    {
                        item.Value.Action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        ReportFault(item.Key, item.Value, e);
                    }
                }

                if (toRemove == null)
                    return;

                foreach (var key in toRemove)
                {
                    // Only if it is still the item that fired: a callback is allowed to have
                    // re-added a timer under the same name, and that one is not spent.
                    if (_timedItems.TryGetValue(key, out var current) && current.Triggered)
                        _timedItems.Remove(key);
                }
            }
        }

        /// <summary>How long a timer stays quiet after a fault of its own has been logged.</summary>
        private const long FaultLogQuietMs = 5000;

        /// <summary>
        /// Per timer, because they fail independently and at their own intervals. A repeating
        /// timer on a 100 ms period whose callback always throws would otherwise write ten lines
        /// a second for as long as the server runs. The first is written in full; after that, at
        /// most one every FaultLogQuietMs, saying how many it stands for.
        /// </summary>
        private static void ReportFault(string name, TimedItem item, Exception e)
        {
            item.FaultsSinceLog++;

            var now = Environment.TickCount64;

            // Not "have I counted more than one": the counter is reset every time one is
            // written, so testing it would let the very next fault through and log every time.
            if (item.FaultLogged && now < item.NextFaultLogTick)
                return;

            var repeat = item.FaultsSinceLog > 1 ? $" ({item.FaultsSinceLog} faults since the last of these)" : "";

            Logger.WriteLog(LogType.Error, $"Timer '{name}' threw and was skipped{repeat}: {e}");

            item.FaultLogged = true;
            item.FaultsSinceLog = 0;
            item.NextFaultLogTick = now + FaultLogQuietMs;
        }

        public void ResetTimer(string name)
        {
            lock (_timedItems)
            {
                if (_timedItems.ContainsKey(name))
                    _timedItems[name].ResetTimer();
            }
        }

        public bool IsTriggered(string name)
        {
            lock (_timedItems)
                if (_timedItems.ContainsKey(name))
                    return _timedItems[name].Triggered;

            return false;
        }
    }
}
