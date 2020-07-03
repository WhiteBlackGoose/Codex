using System.IO;
using Codex.Logging;
using Codex.Search;
using Lucene.Net.Store;
using Directory = Lucene.Net.Store.Directory;

namespace Codex.Lucene.Search
{
    public class LuceneConfiguration : CodexBaseConfiguration
    {
        public string Directory { get; set; }

        public Logger Logger = new ConsoleLogger();

        public string GetIndexRoot(SearchType searchType)
        {
            return Path.Combine(Directory, searchType.IndexName);
        }

        public Directory OpenIndexDirectory(SearchType searchType)
        {
            return FSDirectory.Open(GetIndexRoot(searchType));
        }

        public LuceneConfiguration(string directory)
        {
            Directory = directory;
        }
    }
}
