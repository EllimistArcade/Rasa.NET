namespace Rasa.Packets.Crafting.Client
{
    using Data;
    using Memory;

    /// <summary>RequestRetrieveFinishedCraftItem(kraftwerksId, resultItemId) - sent once a job's timeLeft reaches zero. See client/augmentations/kraftwerks.py.</summary>
    public class RequestRetrieveFinishedCraftItemPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestRetrieveFinishedCraftItem;

        public ulong KraftwerksId { get; set; }
        public ulong ItemId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            KraftwerksId = pr.ReadULong();
            ItemId = pr.ReadULong();
        }
    }
}
