namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_JoinFriendCancelled(friendName, requestId=0): PM_FRIEND_REQUEST_TO_JOIN_CANCELLED. A
    /// non-zero requestId also destroys that request's dialog by name, which is the only way to
    /// take one off the screen - so it is always sent.
    /// </summary>
    public class JoinFriendCancelledPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.JoinFriendCancelled;

        internal string FriendName { get; set; }
        internal uint RequestId { get; set; }

        internal JoinFriendCancelledPacket(string friendName, uint requestId)
        {
            FriendName = friendName;
            RequestId = requestId;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUnicodeString(FriendName);
            pw.WriteUInt(RequestId);
        }
    }
}
