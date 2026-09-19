namespace Rasa.Packets.Communicator.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells the sender their whisper was not delivered.
    ///
    /// Recv_WhisperFailAck(target, pmsgId) (client/communicator.py:896) posts pmsgId straight
    /// to UI_DISPLAY_PLAYER_MESSAGE with {'player': target}, so the second field is a player
    /// message id, not text. It was written as a unicode string, which the client would look
    /// up as a message key and print "missing translation" for. Pick a message whose text
    /// substitutes %(player)s - PmWhisperTargetNotInGame and PmWhisperTargetIgnoringYou do;
    /// PmNoSuchUser and PmUserNotOnline use %(name)s and would print a substitution error.
    /// </summary>
    public class WhisperFailAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.WhisperFailAck;

        public string Target { get; set; }
        public PlayerMessage MessageId { get; set; }

        public WhisperFailAckPacket(string target, PlayerMessage messageId)
        {
            Target = target;
            MessageId = messageId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(Target);
            pw.WriteUInt((uint)MessageId);
        }
    }
}
