namespace Rasa.Packets.Communicator.Both
{
    using Data;
    using Memory;

    public class EmotePacket : PythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.Emote;

        public string Emote { get; set; }
        private string SenderName;
        
        public EmotePacket()
        {
        }

        public EmotePacket(string senderName, string emote)
        {
            SenderName = senderName;
            Emote = emote;
        }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            Emote = pr.ReadUnicodeString();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(SenderName);
            pw.WriteUnicodeString(Emote);
        }
    }
}
