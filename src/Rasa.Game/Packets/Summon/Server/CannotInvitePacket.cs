namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// client/augmentations/manifestation.py Recv_CannotInvite(inviteeName, msgId): prints msgId
    /// with the name as %(player)s. Any player message that interpolates %(player)s will do.
    /// </summary>
    public class CannotInvitePacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.CannotInvite;

        internal string InviteeName { get; set; }
        internal PlayerMessage MsgId { get; set; }

        internal CannotInvitePacket(string inviteeName, PlayerMessage msgId)
        {
            InviteeName = inviteeName;
            MsgId = msgId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(InviteeName);
            pw.WriteUInt((uint)MsgId);
        }
    }
}
