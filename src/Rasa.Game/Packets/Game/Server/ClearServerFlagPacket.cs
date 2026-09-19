namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Turns one flag off - as far as the protocol is concerned.
    ///
    /// The client cannot act on it. serverflagmanager.g_serverFlags is a list, and
    /// Recv_ClearServerFlag calls g_serverFlags.pop(flagId, None); list.pop takes one argument,
    /// so every one of these raises TypeError in the client. entitymanager catches it, logs it
    /// and, on a client with GM enabled, puts a "Python Exception" dialog on screen. The flag is
    /// not removed and SERVER_FLAGS_UPDATED is never posted.
    ///
    /// ServerFlagManager sends this for the sake of the protocol and then sends ServerFlags,
    /// which is what actually takes the flag off the client.
    /// </summary>
    public class ClearServerFlagPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ClearServerFlag;

        public ServerFlag Flag { get; }

        public ClearServerFlagPacket(ServerFlag flag)
        {
            Flag = flag;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt((uint)Flag);
        }
    }
}
