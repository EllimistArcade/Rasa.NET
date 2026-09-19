namespace Rasa.Packets.Social
{
    using Memory;

    internal static class SocialArgs
    {
        /// <summary>
        /// A family name from one of the social by-name calls. The two ways one reaches the
        /// server disagree about the string type: the social window's name box sends
        /// TextEdit.GetText(), a unicode string (0x5_), and the radial menu sends the server's own
        /// ActorName back as a byte string (0x4_). Reading only one of them throws, and the throw
        /// escapes ReadPacket and drops the connection.
        /// </summary>
        public static string ReadName(PythonReader pr)
        {
            var name = pr.PeekType() == PythonType.UnicodeString ? pr.ReadUnicodeString() : pr.ReadString();

            return name?.Trim() ?? string.Empty;
        }
    }
}
