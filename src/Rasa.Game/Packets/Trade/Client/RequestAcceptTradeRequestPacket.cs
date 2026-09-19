namespace Rasa.Packets.Trade.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// SendCallActorMethod('RequestAcceptTradeRequest', ()). Sent when the invited player
    /// clicks the pending-trade status indicator. It names nobody: the server tracks who invited them.
    /// </summary>
    public class RequestAcceptTradeRequestPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestAcceptTradeRequest;

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
        }
    }
}
