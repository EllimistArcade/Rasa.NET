using System.Collections.Generic;

namespace Rasa.Structures
{
    public class ChatChannel
    {
        public char[] Name = new char[40];
        public bool IsDefaultChannel { get; set; }
        public uint MapContextId { get; set; }
        public int InstanceId { get; set; }
        public uint ChannelId { get; set; }                      // 1 - general, 6 - map trade, ...

        /// <summary>
        /// Entity ids of the players in this channel. This was a hand-rolled doubly linked list,
        /// whose append walked a freshly constructed node rather than the one already in the
        /// channel, so the loop ended immediately and every player after the first was linked to
        /// a throwaway object - the channel never held more than one member.
        /// </summary>
        public List<ulong> Players { get; } = new List<ulong>();
    }
}
