namespace Rasa.Packets.Petition.Client
{
    using Data;
    using Memory;

    /// <summary>
    /// client/petitionmanager.py:70 - SendRetrieveKBArticle(kbArticleId). No caller in the
    /// client; see SearchKBPacket.
    /// </summary>
    public class RetrieveKBArticlePacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RetrieveKBArticle;

        internal uint ArticleId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            ArticleId = pr.ReadUInt();
        }
    }
}
