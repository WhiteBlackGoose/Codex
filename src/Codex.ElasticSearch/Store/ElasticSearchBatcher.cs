using Codex.Sdk.Utilities;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatcher
    {
        /// <summary>
        /// Defines stored filters for each entity type
        /// </summary>
        public readonly ElasticSearchStoredFilterBuilder[] CommitSearchTypeStoredFilters = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count];

        /// <summary>
        /// Defines stored filter for declared definitions in the current commit
        /// </summary>
        /// <remarks>
        /// This is an array because Add takes an array not a single item
        /// </remarks>
        public readonly ElasticSearchStoredFilterBuilder[] DeclaredDefinitionStoredFilter;

        /// <summary>
        /// Empty set of stored filters for passing as additional stored filters
        /// </summary>
        internal readonly ElasticSearchStoredFilterBuilder[] EmptyStoredFilters = Array.Empty<ElasticSearchStoredFilterBuilder>();

        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();

        private readonly ElasticSearchService service;
        private readonly ElasticSearchStore store;

        private ElasticSearchBatch currentBatch;

        public ElasticSearchBatcher(ElasticSearchStore store)
        {
            this.store = store;
            service = store.Service;
            currentBatch = new ElasticSearchBatch(this);

            Placeholder.Todo("Initialize fields (i.e. stored filter builders)");

            Placeholder.Todo("Merge stored filters into other supplemental filters (i.e. this filter, the current commit filter, needs to be merged with the repository filter and cumulative commit filter");
        }

        public async ValueTask<None> AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded = null, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            // This part must be synchronous since Add calls this method and
            // assumes that the content id and size will be set
            PopulateContentIdAndSize(entity, store);

            while (true)
            {
                var batch = currentBatch;
                if (!batch.TryAdd(store, entity, onAdded, additionalStoredFilters))
                {
                    if (batch.TryReserveExecute())
                    {
                        currentBatch = new ElasticSearchBatch(this);
                        await store.Store.Service.UseClient(batch.ExecuteAsync);

                        return None.Value;
                    }
                }
                else
                {
                    // Added to batch so batch is not full. Don't need to execute batch yet so return.
                    return None.Value;
                }
            }
        }

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            var backgroundTask = AddAsync(store, entity, additionalStoredFilters: additionalStoredFilters);
            if (!backgroundTask.IsCompleted)
            {
                backgroundTasks.Enqueue(backgroundTask);
            }
        }

        public void PopulateContentIdAndSize<T>(T entity, ElasticSearchEntityStore<T> store)
            where T : class, ISearchEntity
        {
            Placeholder.NotImplemented("Get content id, size, and store content id as Uid where appropriate");
        }

        public async Task FlushAsync()
        {
            // Flush any background operations
            while (backgroundTasks.TryDequeue(out var backgroundTask))
            {
                await backgroundTask;
            }

            // Repository stored filter (just OR the commit stored filter with current repository stored filter at the end)
            // Cumulative commit stored filter (just OR the commit stored filter with parent commit CUMULATIVE stored filters at the end)
            // Cumulative commit stored filters are complicated because prior commits can be indexed after the current commit!
            Placeholder.Todo("Union stored filters for current commit with stored filters for repository and create cumulative commit stored filter");

            await Placeholder.NotImplementedAsync("Flush stored filters. Just make them a background task");

            await Placeholder.NotImplementedAsync("Wait for background operations to complete and flush again");
        }
    }
}
