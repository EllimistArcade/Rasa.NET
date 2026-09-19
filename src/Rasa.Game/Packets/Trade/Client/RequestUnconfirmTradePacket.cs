namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestUnconfirmTrade', ()). The Decline button, and any edit to the
    /// credits field.
    /// </summary>
    public class RequestUnconfirmTradePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestUnconfirmTrade;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
