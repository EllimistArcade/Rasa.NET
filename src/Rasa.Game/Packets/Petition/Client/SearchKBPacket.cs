namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:65 - SendSearchKB(searchText). Nothing in the client calls it,
    /// and Recv_SearchKBAck is an empty body, so this is groundwork against a client that does.
    /// </summary>
    public class SearchKBPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SearchKB;

        internal string SearchText { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();

            // An empty search box marshals as None rather than as a zero-length string, the same
            // way the petition edit boxes do.
            SearchText = pr.PeekType() == PythonType.Structs ? ReadNone(pr) : pr.ReadUnicodeString();
        }

        private static string ReadNone(PythonReader pr)
        {
            pr.ReadNoneStruct();
            return string.Empty;
        }
    }
}
