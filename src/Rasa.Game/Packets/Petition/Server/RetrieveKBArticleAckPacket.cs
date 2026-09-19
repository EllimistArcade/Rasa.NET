namespace Rasa.Packets.Petition.Server
{
    using Data;
    using Memory;
    using Structures;

    /// <summary>
    /// Recv_RetrieveKBArticleAck(success, kbArticleId, articleInfo) -
    /// client/petitionmanager.py:103, empty body.
    ///
    /// The id is written whether or not the article was found, so a client that keeps a request
    /// in flight can match the answer to it. A failure writes None for the article rather than
    /// an empty dictionary: the two are different things, and None is what the rest of this
    /// protocol uses for "there isn't one".
    /// </summary>
    public class RetrieveKBArticleAckPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.RetrieveKBArticleAck;

        public bool Success { get; set; }
        public uint ArticleId { get; set; }
        public KbArticleInfo Article { get; set; }

        public RetrieveKBArticleAckPacket(bool success, uint articleId, KbArticleInfo article = null)
        {
            Success = success;
            ArticleId = articleId;
            Article = article;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(3);
            pw.WriteBool(Success);
            pw.WriteUInt(ArticleId);

            if (Article == null)
                pw.WriteNoneStruct();
            else
                Article.Write(pw);
        }
    }
}
