namespace Rasa.Packets.Minion.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// <c>MinionFollowTarget</c>, which sends <c>(targetId,)</c> - the entity the player had under
    /// the cursor. The client has already checked it is in range and, where it matters, friendly;
    /// the server checks again because the client's word is not a constraint.
    /// </summary>
    public class MinionFollowTargetPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.MinionFollowTarget;

        public ulong TargetId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            TargetId = pr.ReadULong();
        }
    }
}
