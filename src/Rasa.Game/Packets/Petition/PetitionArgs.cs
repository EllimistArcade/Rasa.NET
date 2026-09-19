namespace Rasa.Packets.Petition
{
    using Memory;

    public static class PetitionArgs
    {
        /// <summary>
        /// Reads a string without insisting on which of the two string types the client used.
        ///
        /// The subject and description come straight out of a text edit widget, and the body has
        /// the client version appended to it as a unicode literal, so the pair arrives as one
        /// unicode string and one that may be either. PythonReader's readers each reject the
        /// other type outright, and a throw in here is not a rejected petition - the read is
        /// abandoned mid-payload, the terminator check fails, and Client.Close takes the player
        /// off the server. Dispatching on the type byte is what keeps a typed-in apostrophe from
        /// costing someone their session.
        /// </summary>
        public static string ReadText(PythonReader pr)
        {
            var type = pr.Reader.ReadByte();
            pr.Reader.BaseStream.Position -= 1;

            return (type & 0xF0) == (byte)PythonType.UnicodeString
                ? pr.ReadUnicodeString()
                : pr.ReadString();
        }
    }
}
