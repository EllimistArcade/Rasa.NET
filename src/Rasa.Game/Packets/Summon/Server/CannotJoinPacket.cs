namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_CannotJoin(playerName, msgId): the same shape as CannotInvite, for the goto
    /// direction - the player asked to be summoned and cannot be.
    /// </summary>
    public class CannotJoinPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CannotJoin;

        internal string PlayerName { get; set; }
        internal PlayerMessage MsgId { get; set; }

        internal CannotJoinPacket(string playerName, PlayerMessage msgId)
        {
            PlayerName = playerName;
            MsgId = msgId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(PlayerName);
            pw.WriteUInt((uint)MsgId);
        }
    }
}
