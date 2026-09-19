using System.Collections.Generic;

namespace Rasa.Structures
{
    using Memory;

    /// <summary>
    /// Wire form of shared.lookingforgroupadinfo.LookingForGroupAdInfo, a 10-tuple:
    ///
    ///     (userId, userName, squadSize, squadInfo, minLevel, maxLevel,
    ///      activities, roles, mapIds, continentIds)
    ///
    /// The same tuple travels in both directions. Going up it is what the player typed
    /// into the create-ad or search tab; coming back down it is one row of the search
    /// results, and the server is what fills in userId, userName and squadInfo - the
    /// window that builds the outgoing tuple never sets those three
    /// (client/ui/lookingforgroupwindow.py:557), so they arrive as None every time.
    ///
    /// Values in the incoming direction are read but the identity fields are discarded:
    /// the server already knows who sent the packet, and squadInfo is a live view of the
    /// squad rather than something a client gets to assert.
    /// </summary>
    public class LookingForGroupAdInfo : IPythonDataStruct
    {
        /// <summary>
        /// Most entries kept from any of the four id lists. The window offers a handful of
        /// activities, roles, maps and continents; a list longer than this is not something it
        /// sends, and every entry comes back in every search result that shows the ad.
        /// </summary>
        public const int MaxListEntries = 16;

        public ulong UserId { get; set; }
        public string UserName { get; set; }

        /// <summary>Requested squad size, or null for "no preference".</summary>
        public uint? SquadSize { get; set; }

        /// <summary>
        /// Current squad, written as the same 5-tuple PartyMember uses. Never null on the
        /// way out: the client takes len() of it without a None check
        /// (client/ui/lookingforgroupwindow.py:1096).
        /// </summary>
        public List<PartyMember> SquadInfo { get; set; } = new List<PartyMember>();

        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public List<int> Activities { get; set; } = new List<int>();
        public List<int> Roles { get; set; } = new List<int>();
        public List<uint> MapIds { get; set; } = new List<uint>();
        public List<uint> ContinentIds { get; set; } = new List<uint>();

        public void Read(PythonReader pr)
        {
            pr.ReadTuple();

            // userId / userName / squadInfo are always None from the stock client. They are
            // still consumed by type so a client that does fill them in cannot desync the
            // rest of the tuple; the values are deliberately not kept.
            if (pr.PeekType() == PythonType.Structs)
                pr.ReadUnkStruct();
            else
                pr.ReadULong();

            if (pr.PeekType() == PythonType.Structs)
                pr.ReadUnkStruct();
            else
                pr.ReadUnicodeString();

            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadUnkStruct();
                SquadSize = null;
            }
            else
                SquadSize = pr.ReadUInt();

            ReadSquadInfo(pr);

            MinLevel = pr.ReadInt();
            MaxLevel = pr.ReadInt();

            ReadIntList(pr, Activities);
            ReadIntList(pr, Roles);
            ReadUIntList(pr, MapIds);
            ReadUIntList(pr, ContinentIds);
        }

        public void Write(PythonWriter pw)
        {
            pw.WriteTuple(10);

            pw.WriteULong(UserId);
            pw.WriteUnicodeString(UserName);

            // Unpack() assigns straight into the instance, and _DisplaySearchResults
            // substitutes MAX_PARTY_SIZE itself when squadSize is None, so None is the
            // correct wire value for "no preference" rather than a zero.
            if (SquadSize.HasValue)
                pw.WriteUInt(SquadSize.Value);
            else
                pw.WriteNoneStruct();

            pw.WriteList(SquadInfo.Count);

            foreach (var member in SquadInfo)
                member.Write(pw);

            pw.WriteInt(MinLevel);
            pw.WriteInt(MaxLevel);

            WriteIntList(pw, Activities);
            WriteIntList(pw, Roles);
            WriteUIntList(pw, MapIds);
            WriteUIntList(pw, ContinentIds);
        }

        private void ReadSquadInfo(PythonReader pr)
        {
            SquadInfo.Clear();

            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadUnkStruct();
                return;
            }

            var count = pr.ReadList();

            for (var i = 0; i < count; i++)
            {
                pr.ReadTuple();
                pr.ReadULong();
                pr.ReadUnicodeString();
                pr.ReadUInt();
                pr.ReadUInt();
                pr.ReadBool();
            }
        }

        private static void ReadIntList(PythonReader pr, List<int> target)
        {
            target.Clear();

            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadUnkStruct();
                return;
            }

            var count = pr.ReadList();

            // Read them all so the stream stays in step; keep the first MaxListEntries.
            for (var i = 0; i < count; i++)
            {
                var value = pr.ReadInt();

                if (target.Count < MaxListEntries)
                    target.Add(value);
            }
        }

        private static void ReadUIntList(PythonReader pr, List<uint> target)
        {
            target.Clear();

            if (pr.PeekType() == PythonType.Structs)
            {
                pr.ReadUnkStruct();
                return;
            }

            var count = pr.ReadList();

            for (var i = 0; i < count; i++)
            {
                var value = pr.ReadUInt();

                if (target.Count < MaxListEntries)
                    target.Add(value);
            }
        }

        private static void WriteIntList(PythonWriter pw, List<int> values)
        {
            pw.WriteList(values.Count);

            foreach (var value in values)
                pw.WriteInt(value);
        }

        private static void WriteUIntList(PythonWriter pw, List<uint> values)
        {
            pw.WriteList(values.Count);

            foreach (var value in values)
                pw.WriteUInt(value);
        }
    }
}
