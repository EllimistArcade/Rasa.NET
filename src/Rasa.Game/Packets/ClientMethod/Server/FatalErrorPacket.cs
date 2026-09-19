using System.Collections.Generic;

namespace Rasa.Packets.ClientMethod.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/clientmethod.py Recv_FatalError(msgId, args = {}): a modal dialog with a single OK
    /// button whose callable is OnRequestExit - pressing it calls PostQuitRequest and the client
    /// exits. This is the end of the session, not a warning, so it is only worth sending to a
    /// player who is about to lose their connection anyway, and it says why.
    ///
    /// It must be sent without the packet queue. The queue is drained by the MainLoop, and every
    /// path that ends a session calls Client.Close, which closes the socket there and then - a
    /// queued fatal error would never reach the wire. CommunicatorManager.FatalError sends it
    /// immediately for that reason.
    /// </summary>
    public class FatalErrorPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.FatalError;

        public PlayerMessage MsgId { get; set; }
        public Dictionary<string, string> Args { get; set; }

        internal FatalErrorPacket(PlayerMessage msgId, Dictionary<string, string> args = null)
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
