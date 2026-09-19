namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_JoinFriendDeclined(friendName): PM_REQUEST_TO_JOIN_DECLINED, told to the asker.
    /// </summary>
    public class JoinFriendDeclinedPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.JoinFriendDeclined;

        internal string FriendName { get; set; }

        internal JoinFriendDeclinedPacket(string friendName)
        {
            FriendName = friendName;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(1);
            pw.WriteUnicodeString(FriendName);
        }
    }
}
