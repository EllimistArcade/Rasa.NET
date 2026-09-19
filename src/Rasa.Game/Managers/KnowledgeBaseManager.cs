using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rasa.Managers
{
    using Structures;

    /// <summary>
    /// The knowledge base the client can ask about but never does.
    ///
    /// Articles are operator-written prose rather than anything the game generates, so they live
    /// in a JSON file beside appsettings.json rather than in a table: there is no schema to keep
    /// in step, nothing writes one at runtime, and a title and a body are pleasanter to author in
    /// a text editor than in a SQL insert. GameDataConfig.KnowledgeBaseFile names it, the path
    /// is relative to the server's working directory, and `kb reload` on the console picks up an
    /// edit without a restart.
    ///
    /// Nothing in the shipped client will display any of this - SendSearchKB and
    /// SendRetrieveKBArticle have no callers, and both Recv_ handlers are a bare `return`. This
    /// exists so a rebuilt client has a server to talk to.
    /// </summary>
    public class KnowledgeBaseManager
    {
        private static KnowledgeBaseManager _instance;
        private static readonly object InstanceLock = new object();

        public static KnowledgeBaseManager Instance
        {
            get
            {
                if (_instance == null)
                    lock (InstanceLock)
                        _instance ??= new KnowledgeBaseManager();

                return _instance;
            }
        }

        private KnowledgeBaseManager()
        {
        }

        /// <summary>
        /// As many articles as one SearchKBAck carries. Titles are short, but the socket buffer
        /// is 8 KB and a search with no text matches every article there is.
        /// </summary>
        public const int SearchResultLimit = 25;

        private readonly Dictionary<uint, KbArticleInfo> _articles = new Dictionary<uint, KbArticleInfo>();
        private readonly object _articleLock = new object();

        public string LoadedFrom { get; private set; }

        public int Count
        {
            get { lock (_articleLock) return _articles.Count; }
        }

        /// <summary>
        /// Reads the article file. Returns false and leaves whatever was already loaded in place
        /// if it cannot be read - a typo in an edit should not empty the knowledge base out from
        /// under a running server.
        /// </summary>
        public bool Load(string path, out string problem)
        {
            problem = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                problem = "no knowledge base file is configured";
                return false;
            }

            if (!File.Exists(path))
            {
                // Not having one is a perfectly ordinary state, not a fault.
                problem = $"{path} does not exist";
                return false;
            }

            List<KbArticleFile> parsed;

            try
            {
                parsed = JsonSerializer.Deserialize<List<KbArticleFile>>(File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });
            }
            catch (Exception e)
            {
                problem = $"{path} could not be read: {e.Message}";
                return false;
            }

            if (parsed == null)
            {
                problem = $"{path} holds no articles";
                return false;
            }

            var articles = new Dictionary<uint, KbArticleInfo>();

            foreach (var entry in parsed)
            {
                if (entry == null)
                    continue;

                if (entry.Id == 0)
                {
                    Logger.WriteLog(LogType.Error,
                        $"Knowledge base: an article with no id (\"{entry.Title}\") was skipped. Ids start at 1.");
                    continue;
                }

                if (articles.ContainsKey(entry.Id))
                {
                    Logger.WriteLog(LogType.Error,
                        $"Knowledge base: article {entry.Id} appears more than once; the later one was skipped.");
                    continue;
                }

                articles.Add(entry.Id, new KbArticleInfo
                {
                    Id = entry.Id,
                    Title = entry.Title ?? string.Empty,
                    Body = entry.Body ?? string.Empty,
                    Keywords = entry.Keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? new List<string>()
                });
            }

            lock (_articleLock)
            {
                _articles.Clear();

                foreach (var article in articles)
                    _articles.Add(article.Key, article.Value);

                LoadedFrom = path;
            }

            Logger.WriteLog(LogType.Initialize, $"Knowledge base: {articles.Count} article(s) from {path}.");
            return true;
        }

        /// <summary>Articles matching the search text, by id, capped.</summary>
        public List<KbArticleInfo> Search(string searchText)
        {
            lock (_articleLock)
                return _articles.Values
                                .Where(a => a.Matches(searchText))
                                .OrderBy(a => a.Id)
                                .Take(SearchResultLimit)
                                .ToList();
        }

        public KbArticleInfo Get(uint id)
        {
            lock (_articleLock)
                return _articles.TryGetValue(id, out var article) ? article : null;
        }

        public List<KbArticleInfo> All()
        {
            lock (_articleLock)
                return _articles.Values.OrderBy(a => a.Id).ToList();
        }

        /// <summary>One article as it is written in the file.</summary>
        private class KbArticleFile
        {
            public uint Id { get; set; }
            public string Title { get; set; }
            public string Body { get; set; }
            public List<string> Keywords { get; set; }
        }
    }
}
