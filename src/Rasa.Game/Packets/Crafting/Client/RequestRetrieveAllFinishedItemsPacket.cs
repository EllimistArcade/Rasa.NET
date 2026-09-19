namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestRetrieveAllFinishedItems(kraftwerksId). See client/augmentations/kraftwerks.py.</summary>
    public class RequestRetrieveAllFinishedItemsPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestRetrieveAllFinishedItems;

        public ulong KraftwerksId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
        }
    }
}
