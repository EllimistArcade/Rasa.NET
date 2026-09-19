using System;
using System.Collections.Generic;
using System.Linq;

namespace Rasa.Structures
{
    using Memory;

    /// <summary>
    /// One knowledge-base article: the third argument of RetrieveKBArticleAck, and the element
    /// type of SearchKBAck's result list.
    ///
    /// **This shape is ours, not the client's**, for the same reason PetitionInfo's is.
    /// `articleInfo` occurs once in the whole client, as a parameter name on
    /// `Recv_RetrieveKBArticleAck`, whose body is a bare `return`. There is no structure
    /// definition, no UI, and nothing calls `SendSearchKB` or `SendRetrieveKBArticle` anywhere
    /// in the client's 1,339 modules - the knowledge base moved to the web before this build
    /// shipped, and the Help window says so. Whatever reads one of these will be a client
    /// written against this definition.
    ///
    /// A dictionary rather than a tuple, like PetitionInfo: the positional convention exists
    /// because the client's own Unpack() assigns positionally, and that does not apply to a
    /// format nobody has pinned down. A dictionary can gain a field without breaking readers.
    /// </summary>
    public class KbArticleInfo
    {
        public uint Id { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>Extra words a search should match beyond the title and body.</summary>
        public List<string> Keywords { get; set; } = new List<string>();

        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Whether this article answers the given search text. Matched against the title, the
        /// keywords and the body, case-insensitively; empty search text matches everything, so
        /// an empty box lists the whole base rather than nothing.
        /// </summary>
        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            search = search.Trim();

            return Contains(Title, search)
                   || Contains(Body, search)
                   || Keywords.Any(k => Contains(k, search));
        }

        private static bool Contains(string haystack, string needle)
        {
            return haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// A search result leaves the body out. Bodies are the whole weight of an article and a
        /// list of them would be a frame several times the 8 KB socket buffer - the likeliest
        /// reason the protocol has both a search and a retrieve rather than one call, the same
        /// as it does for petitions.
        /// </summary>
        public void Write(PythonWriter pw, bool includeBody = true)
        {
            pw.WriteDictionary(includeBody ? 4 : 3);

            pw.WriteString("id");
            pw.WriteUInt(Id);

            pw.WriteString("title");
            pw.WriteUnicodeString(Title);

            pw.WriteString("keywords");
            pw.WriteList(Keywords.Count);

            foreach (var keyword in Keywords)
                pw.WriteUnicodeString(keyword);

            if (!includeBody)
                return;

            pw.WriteString("body");
            pw.WriteUnicodeString(Body);
        }
    }
}
