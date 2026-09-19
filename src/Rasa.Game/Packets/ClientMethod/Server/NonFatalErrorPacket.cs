using System.Collections.Generic;

namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py Recv_NonFatalError(msgId, args = {}): the same modal dialog with an
    /// OK button that does nothing but close it. The session continues.
    ///
    /// Modal is the whole point of it, and the reason to use it sparingly: while it is up the rest
    /// of the UI is suppressed. Anything the player can carry on past belongs in the chat window
    /// (DisplayClientMessage), not here.
    /// </summary>
    public class NonFatalErrorPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.NonFatalError;

        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }

        internal NonFatalErrorPacket(PlayerMessage msgId, Dictionary<string, string> args = null)
        {
            MsgId = msgId;
            Args = args ?? new Dictionary<string, string>();
        }

        public override void Write(PythonWriter pw)
        {
            // args has a default client-side, but it is always written: several player messages
            // interpolate named values, and an absent dictionary leaves the placeholder on screen.
            pw.WriteTuple(2);
            pw.WriteUInt((uint)MsgId);
            pw.WriteDictionary(Args.Count);

            foreach (var arg in Args)
            {
                pw.WriteString(arg.Key);
                pw.WriteString(arg.Value);
            }
        }
    }
}
