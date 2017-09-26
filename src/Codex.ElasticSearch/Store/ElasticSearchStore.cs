using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Nest;
using Codex.Storage.DataModel;
using static Codex.Utilities.SerializationUtilities;
using Codex.Utilities;
using Codex.Analysis;

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

    internal class ElasticSearchBatch
    {
        public BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            var response = await context.Client.BulkAsync(BulkDescriptor);
            throw new NotImplementedException();
        }

        public class Item
        {
            public int Index { get; set; }
            public ISearchEntity Entity { get; set; }
            public ElasticSearchEntityStore EntityStore { get; set; }
            public int[] AdditionalStoredFilterBuilderIds { get; set; }
        }
    }

    internal class ElasticSearchBatcher
    {
        // Commit stored filters (one per entity type)
        // Declared definition stored filter
        // Referenced definition stored filter
        // Repository stored filter (just OR the commit stored filter with current repository stored filter at the end)
        // Cumulative commit stored filter (just OR the commit stored filter with current commit stored filter at the end)
        public ElasticSearchStoredFilterBuilder[] StoredFiltersBuilders = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count + 10];

        public Task AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, Action<T> onAdded = null)
            where T : class, ISearchEntity
        {
            PopulateContentIdAndSize(entity, store);
            return Placeholder.NotImplementedAsync();
        }

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity)
            where T : class, ISearchEntity
        {
            PopulateContentIdAndSize(entity, store);
        }

        public void PopulateContentIdAndSize<T>(T entity, ElasticSearchEntityStore<T> store)
            where T : class, ISearchEntity
        {
            Placeholder.NotImplemented("Get content id, size, and store content id as Uid where appropriate");
        }

        public async Task FlushAsync()
        {
            await Placeholder.NotImplementedAsync("Flush entities");
            await Placeholder.NotImplementedAsync("Trigger onAdded callbacks");
            await Placeholder.NotImplementedAsync("Flush stored filters");
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
