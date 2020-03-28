using Codex.Logging;

namespace Codex.Lucene.Search
{
    public class LuceneConfiguration
    {
        public string Directory { get; set; }

        public Logger Logger = new ConsoleLogger();

        public LuceneConfiguration(string directory)
        {
            Directory = directory;
        }
    }
}
