using System;
using System.Collections.Generic;

namespace Rasa.Structures
{
    /// <summary>
    /// One live looking-for-group ad, keyed by the placing account.
    ///
    /// Deliberately holds no squad roster. The client renders an ad's open slots as
    /// squadSize - len(squadInfo) (client/ui/lookingforgroupwindow.py:1096) and lists the
    /// members by name, so a roster captured when the ad was placed would show people who
    /// have since left. The roster is rebuilt from the live party when a search asks for it.
    /// </summary>
    public class LookingForGroupAd
    {
        public uint AccountId { get; set; }
        public ulong LeaderEntityId { get; set; }
        public string LeaderName { get; set; }

        /// <summary>Party the ad recruits for, or 0 when the leader is soloing.</summary>
        public uint PartyId { get; set; }

        /// <summary>Requested squad size, or null for "no preference".</summary>
        public uint? SquadSize { get; set; }

        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public List<int> Activities { get; set; } = new List<int>();
        public List<int> Roles { get; set; } = new List<int>();
        public List<uint> MapIds { get; set; } = new List<uint>();
        public List<uint> ContinentIds { get; set; } = new List<uint>();

        public DateTime PlacedAt { get; set; }
    }
}
