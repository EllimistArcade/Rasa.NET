namespace Rasa.Packets.Summon
{
    using Memory;

    internal static class SummonArgs
    {
        /// <summary>
        /// An invitation or request id, handed out by the server and echoed back. The client stores
        /// it untouched, so it comes back in whatever shape it was sent, but a console call could
        /// send a long. Anything out of range reads as 0, which matches no pending summon.
        /// </summary>
        public static uint ReadId(PythonReader pr)
        {
            long value;

            switch (pr.PeekType())
            {
                case PythonType.Long:
                    value = pr.ReadLong();
                    break;
                case PythonType.Structs:
                    pr.ReadUnkStruct();
                    return 0;
                default:
                    value = pr.ReadInt();
                    break;
            }

            return value > 0 && value <= uint.MaxValue ? (uint)value : 0;
        }

        /// <summary>
        /// Accept or decline. The client's dialogs pass the ints 1 and 0, not True and False
        /// (manifestation.py OnAcceptJoinFriendInvitation), so this cannot be a plain ReadBool:
        /// that accepts the inline forms 0x10 and 0x11 but throws on a wider int encoding, and a
        /// throw in Read costs the player their connection. Only a non-zero value accepts.
        /// </summary>
        public static bool ReadResponse(PythonReader pr)
        {
            switch (pr.PeekType())
            {
                case PythonType.Structs:
                    // None, True and Zero all live here; ReadBool is the only reader that
                    // distinguishes them, and they are the only three values it can meet.
                    return pr.ReadBool();
                case PythonType.Long:
                    return pr.ReadLong() != 0;
                default:
                    return pr.ReadInt() != 0;
            }
        }
    }
}
