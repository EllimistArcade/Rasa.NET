namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Turns one flag on. The client appends it to its list if it is not already there, so this
    /// can be sent more than once without harm.
    /// </summary>
    public class SetServerFlagPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SetServerFlag;

        public ServerFlag Flag { get; }

        public SetServerFlagPacket(ServerFlag flag)
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
