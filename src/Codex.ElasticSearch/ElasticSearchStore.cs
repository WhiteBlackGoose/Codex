using Codex.Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    class ElasticSearchStore : IStore
    {
        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchStore(ElasticSearchStoreConfiguration configuration)
        {

        }

        public async Task FinalizeAsync()
        {
            // Finalize commits. Should there be a notion of sessions for commits
            // rather than having the entire store be commit specific
            throw new NotImplementedException();
        }

        public async Task InitializeAsync()
        {
            // Create indices with appropriate mappings


            throw new NotImplementedException();
        }
    }

    class ElasticSearchStoreConfiguration
    {
        public string Prefix;
    }
}
