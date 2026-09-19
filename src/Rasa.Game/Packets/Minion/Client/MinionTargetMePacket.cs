namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionTargetMe</c>, sent with no arguments. Which minion it applies to is the server's
    /// to work out: the client has no idea which entity belongs to it.
    /// </summary>
    public class MinionTargetMePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionTargetMe;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
