namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionTarget</c>, which sends <c>(targetId,)</c> - the entity the player had under
    /// the cursor. The client has already checked it is in range and, where it matters, friendly;
    /// the server checks again because the client's word is not a constraint.
    /// </summary>
    public class MinionTargetPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionTarget;

        public ulong TargetId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            TargetId = pr.ReadULong();
        }
    }
}
