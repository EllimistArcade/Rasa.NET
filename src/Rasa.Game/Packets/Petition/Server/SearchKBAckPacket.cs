using System.Collections.Generic;

namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Recv_SearchKBAck(success, resultList) - client/petitionmanager.py:99, empty body.
    ///
    /// resultList is a list of KbArticleInfo dictionaries without their bodies, which is this
    /// server's definition - the client has none. See KbArticleInfo.
    /// </summary>
    public class SearchKBAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.SearchKBAck;

        public bool Success { get; set; }
        public List<KbArticleInfo> Results { get; set; }

        public SearchKBAckPacket(bool success, List<KbArticleInfo> results = null)
        {
            Success = success;
            Results = results ?? new List<KbArticleInfo>();
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteBool(Success);

            pw.WriteList(Results.Count);

            foreach (var result in Results)
                result.Write(pw, false);
        }
    }
}
