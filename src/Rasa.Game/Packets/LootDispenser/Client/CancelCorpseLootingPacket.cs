namespace Rasa.Packets.LootDispenser.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// The corpse window has closed. lootdispenser.CancelCorpseLooting sends (self.entityId,) -
    /// the dispenser's own id, not the player's - and corpselootwindow sends it on close, so it
    /// arrives on an ordinary window close as much as on a deliberate cancel. It expects no
    /// answer.
    /// </summary>
    public class CancelCorpseLootingPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CancelCorpseLooting;

        public ulong EntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            EntityId = pr.ReadULong();
        }
    }
}
