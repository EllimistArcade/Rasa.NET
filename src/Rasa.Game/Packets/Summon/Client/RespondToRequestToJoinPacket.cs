namespace Rasa.Packets.Summon.Client
{
    using Data;
    using Memory;
    using Party;

    /// <summary>
    /// manifestation.py RespondToRequestToJoin: (requestId, response). The requestId comes back
    /// from the dialog, which is named per request, so several can be open at once.
    /// </summary>
    public class RespondToRequestToJoinPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RespondToRequestToJoin;

        internal uint RequestId { get; set; }
        internal bool Accepted { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            RequestId = SummonArgs.ReadId(pr);
            Accepted = SummonArgs.ReadResponse(pr);
        }
    }
}
