using Codex.Logging;
using Codex.Search;

namespace Codex.Lucene.Search
{
    public class LuceneConfiguration : CodexBaseConfiguration
    {
        public string Directory { get; set; }

        public Logger Logger = new ConsoleLogger();

        public LuceneConfiguration(string directory)
        {
            Directory = directory;
        }
    }
}
