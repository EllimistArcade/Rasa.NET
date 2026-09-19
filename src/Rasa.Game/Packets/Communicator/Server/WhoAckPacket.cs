namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// One matching player, sent once per result.
    ///
    /// Recv_WhoAck tests ClanName, TitleId and CurrentGameContextOrdinal against None and
    /// only formats them when they are set, so absent values must be written as a real None
    /// struct. WriteUnicodeString(null) emits an empty unicode string, not None, which would
    /// make the client render an empty clan name rather than omit the clan entirely.
    /// </summary>
    public class WhoAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.WhoAck;

        public string CharacterName { get; set; }
        public string FamilyName { get; set; }
        public string ClanName { get; set; }
        public uint? TitleId { get; set; }
        public uint CharacterClass { get; set; }
        public uint Level { get; set; }
        public uint ContextId { get; set; }
        public uint? CurrentGameContextOrdinal { get; set; }
        public bool IsAfk { get; set; }
        public bool IsTrialAccount { get; set; }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(10);
            pw.WriteUnicodeString(CharacterName);
            pw.WriteUnicodeString(FamilyName);

            if (string.IsNullOrEmpty(ClanName))
                pw.WriteNoneStruct();
            else
                pw.WriteUnicodeString(ClanName);

            if (TitleId.HasValue)
                pw.WriteUInt(TitleId.Value);
            else
                pw.WriteNoneStruct();

            pw.WriteUInt(CharacterClass);
            pw.WriteUInt(Level);
            pw.WriteUInt(ContextId);

            if (CurrentGameContextOrdinal.HasValue)
                pw.WriteUInt(CurrentGameContextOrdinal.Value);
            else
                pw.WriteNoneStruct();

            pw.WriteBool(IsAfk);
            pw.WriteBool(IsTrialAccount);
        }
    }
}
