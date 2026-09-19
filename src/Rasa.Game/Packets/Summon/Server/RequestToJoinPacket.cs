namespace Rasa.Packets.Summon.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// Recv_RequestToJoin(requestId, friendName): ID_FRIEND_REQUESTS_TO_JOIN, "<name> wishes to join
    /// you at your current location", with Accept and Decline. The dialog is named per requestId,
    /// so unlike the summon direction a player can hold several of these at once.
    /// </summary>
    public class RequestToJoinPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RequestToJoin;

        internal uint RequestId { get; set; }
        internal string FriendName { get; set; }

        internal RequestToJoinPacket(uint requestId, string friendName)
        {
            RequestId = requestId;
            FriendName = friendName;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteUInt(RequestId);
            pw.WriteUnicodeString(FriendName);
        }
    }
}
