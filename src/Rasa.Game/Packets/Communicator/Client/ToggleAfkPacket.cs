namespace Rasa.Packets.Communicator.Client
{
    using Data;
    using Memory;

    public class ToggleAfkPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ToggleAfk;

        // 0 Elements - client sends SendCallActorMethod('ToggleAfk', ())
        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
