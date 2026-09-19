namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// A whisper addressed to the sender's own family name. Recv_WhisperSelf(msg) prints
    /// PM_WHISPER_SELF, "A voice inside your head says: %(message)s".
    /// </summary>
    public class WhisperSelfPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.WhisperSelf;
        
        public string Message { get; set; }

        public WhisperSelfPacket(string message)
        {
            Message = message;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUnicodeString(Message);
        }
    }
}
