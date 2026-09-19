namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The sender's own copy of a delivered whisper. Recv_WhisperAck(target, msg, isTargetAfk)
    /// prints PM_CHAT_WHISPER, "To %(username)s: %(speech)s", and marks the name when the
    /// target is AFK.
    /// </summary>
    public class WhisperAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.WhisperAck;

        public string FamilyName { get; set; }
        public string Message { get; set; }
        public bool IsAfk { get; set; }

        public WhisperAckPacket(string familyName, string message, bool isAfk)
        {
            FamilyName = familyName;
            Message = message;
            IsAfk = isAfk;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteUnicodeString(FamilyName);
            pw.WriteUnicodeString(Message);
            pw.WriteBool(IsAfk);
        }
    }
}
