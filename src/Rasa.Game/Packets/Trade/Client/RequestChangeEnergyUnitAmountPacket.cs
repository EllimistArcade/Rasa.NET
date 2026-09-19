namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestChangeEnergyUnitAmount', (energyUnits,)). The credits this
    /// player offers. The client clamps it to 0..GetFunds() but that is not trusted.
    /// </summary>
    public class RequestChangeEnergyUnitAmountPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestChangeEnergyUnitAmount;

        public long Amount { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            Amount = TradeArgs.ReadInteger(pr);
        }
    }
}
