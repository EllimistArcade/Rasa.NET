namespace Rasa.Packets.Minion.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Tells the client a minion has appeared.
    ///
    /// <c>Recv_MinionAdded(self, *args)</c> takes anything and reads none of it - all it does is
    /// raise the MINION_SUMMONED tutorial, "Subordinate Summoned: You have summoned a subordinate
    /// that you may give commands to." So the shape is ours, and the minion's entity id is what a
    /// later client would want, so that is what goes in.
    ///
    /// The tutorial is one-shot per character, so this is not a reliable signal that anything
    /// happened; it is not a substitute for the acks.
    /// </summary>
    public class MinionAddedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionAdded;

        public ulong MinionId { get; }

        public MinionAddedPacket(ulong minionId)
        {
            MinionId = minionId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteULong(MinionId);
        }
    }
}
