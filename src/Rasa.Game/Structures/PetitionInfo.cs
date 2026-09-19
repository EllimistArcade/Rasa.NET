using System;

namespace Rasa.Structures
{
    using Data;
    using Memory;
    using Structures.Char;

    /// <summary>
    /// The third argument of RetrievePetitionAck.
    ///
    /// **This shape is ours, not the client's.** `petitionInfo` occurs exactly once in the whole
    /// client - as a parameter name on `Recv_RetrievePetitionAck`, whose body is empty. There is
    /// no structure definition, no UI that reads one, and nothing that sends
    /// `RetrievePetition` in the first place, so there is no original format to match and
    /// nothing to check a guess against. Whatever reads this will be a client written against
    /// this definition.
    ///
    /// Written as a **dictionary**, which is a deliberate departure from the rest of the server:
    /// every other composite here is a positional tuple, because the client's own `Unpack()`
    /// assigns positionally and the order was not ours to pick. That reason does not apply to a
    /// format nobody has pinned down, and a format that is explicitly groundwork will grow
    /// fields - a dictionary lets it, where a tuple would break every reader each time one is
    /// added.
    ///
    /// Only ever sent to the account that filed the petition, so it carries what that player
    /// would be shown and not the operator-facing columns (account, character, coordinates)
    /// they already know or have no use for.
    ///
    /// Not an IPythonDataStruct: that interface carries a Read as well, and nothing reads one of
    /// these. This only ever goes out.
    /// </summary>
    public class PetitionInfo
    {
        public uint Id { get; set; }
        public PetitionType Type { get; set; }
        public PetitionStatus Status { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }

        /// <summary>Unix seconds. A long rather than an int: an int runs out in 2038.</summary>
        public long FiledAt { get; set; }

        /// <summary>What was done about it. Empty while it is still open.</summary>
        public string Resolution { get; set; }

        public PetitionInfo()
        {
        }

        public PetitionInfo(PetitionEntry entry)
        {
            Id = entry.Id;
            Type = (PetitionType)entry.Type;
            Status = (PetitionStatus)entry.Status;
            Summary = entry.Summary;
            Body = entry.Body;
            FiledAt = new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
            Resolution = entry.Resolution ?? string.Empty;
        }

        /// <summary>
        /// A search result leaves the body out. Bodies are the big part of a petition and a list
        /// of them would be a frame several times the 8 KB socket buffer - which is the likeliest
        /// reason the protocol has both a search and a retrieve rather than one call: the list
        /// tells you what is there, RetrievePetition fetches one in full.
        /// </summary>
        public void Write(PythonWriter pw, bool includeBody = true)
        {
            pw.WriteDictionary(includeBody ? 7 : 6);

            pw.WriteString("id");
            pw.WriteUInt(Id);

            pw.WriteString("type");
            pw.WriteUInt((uint)Type);

            pw.WriteString("status");
            pw.WriteUInt((uint)Status);

            pw.WriteString("summary");
            pw.WriteUnicodeString(Summary);

            if (includeBody)
            {
                pw.WriteString("body");
                pw.WriteUnicodeString(Body);
            }

            pw.WriteString("filedAt");
            pw.WriteLong(FiledAt);

            pw.WriteString("resolution");
            pw.WriteUnicodeString(Resolution);
        }
    }
}
