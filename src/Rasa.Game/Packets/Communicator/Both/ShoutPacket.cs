namespace Rasa.Packets.Communicator.Both
{
    using Data;
    using Memory;

    /// <summary>
    /// Client -> server: the shouted text.
    /// Server -> client: (senderFamilyName, text).
    ///
    /// Note there is no entity id, unlike RadialChat. Recv_Shout only writes to the chat
    /// window, where Recv_RadialChat additionally posts UI_DISPLAY_CHAT_BUBBLE against the
    /// sender's entity - which requires that entity to be visible. Shout is therefore meant
    /// to carry past visibility range (client/communicator.py:925 and :952).
    /// </summary>
    public class ShoutPacket : PythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.Shout;

        public string FamilyName { get; set; }
        public string TextMsg { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            TextMsg = pr.ReadUnicodeString();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(FamilyName);
            pw.WriteUnicodeString(TextMsg);
        }
    }
}
