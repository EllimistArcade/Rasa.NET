namespace Rasa.Packets.Social.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/communicator.py:479 - SendChatMsg('AddFriend', (arg,)).
    ///
    /// The id is an account id (the client's userId). The docstring says "by character id",
    /// but its siblings RemoveFriend and AddIgnore carry the same docstring and are sent with
    /// the socialwindow row's userId (socialwindow.py:771/781), every friend and ignore row is
    /// keyed by userId (social.py:158/201), and the server's by-id packets already read an
    /// account id. No shipped UI calls AddFriend; it is reached from the Python console or
    /// scripts, so the argument is whatever Python integer was typed.
    ///
    /// A Python int marshals as 0x1_, a long (or an id the server sent with WriteULong) as
    /// 0x2_, and zero may arrive as the Zero struct. Anything that is not a valid uint leaves
    /// AccountId null, which the manager acks as a failed add. Read must still consume the
    /// value: CallServerMethodMessage checks for the 0x66 terminator after it and drops the
    /// connection if it is not there.
    /// </summary>
    public class AddFriendPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode => GameOpcode.AddFriend;

        public uint? AccountId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            long value;

            switch (pr.PeekType())
            {
                case PythonType.Long:
                    value = pr.ReadLong();
                    break;

                case PythonType.Structs:
                    AccountId = null;
                    pr.ReadUnkStruct();
                    return;

                default:
                    value = pr.ReadInt();
                    break;
            }

            AccountId = value > 0 && value <= uint.MaxValue ? (uint)value : null;
        }
    }
}
