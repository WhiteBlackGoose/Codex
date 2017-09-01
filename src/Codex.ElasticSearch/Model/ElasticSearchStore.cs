using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    class ElasticSearchStore : StoreBase
    {
        internal readonly ElasticSearchService Service;
        internal readonly ElasticSearchStoreConfiguration Configuration;

        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchStore(ElasticSearchStoreConfiguration configuration, ElasticSearchService service)
        {
            Configuration = configuration;
            Service = service;
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
            if (Configuration.CreateIndices)
            {
            }

            throw new NotImplementedException();
        }

        public override Task<IStore<TSearchType>> CreateStoreAsync<TSearchType>(SearchType searchType)
        {
            var store = new TypedStore<TSearchType>(this, searchType);
            throw new NotImplementedException();
        }
    }

    class ElasticSearchStoreConfiguration
    {
        public string Prefix;
        public bool CreateIndices = true;
    }
}
