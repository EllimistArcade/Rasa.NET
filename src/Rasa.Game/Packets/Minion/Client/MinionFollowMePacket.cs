namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionFollowMe</c>, sent with no arguments. Which minion it applies to is the server's
    /// to work out: the client has no idea which entity belongs to it.
    /// </summary>
    public class MinionFollowMePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionFollowMe;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
