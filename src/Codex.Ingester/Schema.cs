using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Ingester
{
    public class RepoIngestionDefinition
    {
        public string name { get; set; }
        public string kind { get; set; }
        public string url { get; set; }
        public string project { get; set; }
        public int id { get; set; }
        public string pat { get; set; }
        public bool ignoreResultFilter { get; set; }
    }

    public class RepoList
    {
        public bool ignoreResultFilter { get; set; }
        public List<RepoIngestionDefinition> repos { get; set; }
    }
}
