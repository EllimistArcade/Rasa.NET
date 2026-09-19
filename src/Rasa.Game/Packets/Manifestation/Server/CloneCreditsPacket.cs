namespace Rasa.Packets.Manifestation.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// The player's clone credit count, for the attributes window and the tier-select screen.
    /// Sent to the manifestation; the character selection pod has its own CloneCreditsChanged.
    /// </summary>
    public class CloneCreditsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CloneCredits;

        public uint CloneCredits { get; set; }

        public CloneCreditsPacket(uint cloneCredits)
        {
            CloneCredits = cloneCredits;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUInt(CloneCredits);
        }
    }
}
