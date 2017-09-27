using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Storage.DataModel;
using static Codex.Utilities.SerializationUtilities;
using Codex.Utilities;
using Codex.Analysis;
using System.Collections.Concurrent;

namespace Codex.ElasticSearch
{
    public partial class ElasticSearchStore : ICodexStore
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

        public async Task<ElasticSearchEntityStore<TSearchType>> CreateStoreAsync<TSearchType>(SearchType searchType)
            where TSearchType : class
        {
            var store = new ElasticSearchEntityStore<TSearchType>(this, searchType);
            await store.InitializeAsync();
            return store;
        }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(IRepository repository, ICommit commit, IBranch branch)
        {
            throw new NotImplementedException();
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
