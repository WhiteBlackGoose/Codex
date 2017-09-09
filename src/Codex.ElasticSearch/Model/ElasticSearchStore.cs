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
    public class ElasticSearchStore : StoreBase
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

        public override Task FinalizeAsync()
        {
            // TODO: Delta commits.
            // TODO: Finalize commits. Should there be a notion of sessions for commits
            // rather than having the entire store be commit specific
            return base.FinalizeAsync();
        }

        public override Task InitializeAsync()
        {
            return base.InitializeAsync();
        }

        public override async Task<IStore<TSearchType>> CreateStoreAsync<TSearchType>(SearchType searchType)
        {
            var store = new TypedStore<TSearchType>(this, searchType);
            await store.InitializeAsync();
            return store;
        }
    }

    public class ElasticSearchStoreConfiguration
    {
        /// <summary>
        /// Prefix for indices
        /// </summary>
        public string Prefix;

        /// <summary>
        /// Indicates where indices should be created when <see cref="ElasticSearchStore.InitializeAsync"/> is called.
        /// </summary>
        public bool CreateIndices = true;

        /// <summary>
        /// The number of shards for created indices
        /// </summary>
        public int? ShardCount;
    }
}
