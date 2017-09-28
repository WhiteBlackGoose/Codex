using Codex.Sdk.Utilities;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatcher
    {
        // Commit stored filters (one per entity type)
        // Declared definition stored filter
        // Referenced definition stored filter
        // Repository stored filter (just OR the commit stored filter with current repository stored filter at the end)
        // Cumulative commit stored filter (just OR the commit stored filter with parent commit stored filters at the end)
        public ElasticSearchStoredFilterBuilder[] StoredFiltersBuilders = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count + 10];

        private ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();

        private ElasticSearchBatch currentBatch = new ElasticSearchBatch();

        private ElasticSearchService service;

        public async ValueTask<None> AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded = null)
            where T : class, ISearchEntity
        {
            // This part must be synchronous since Add calls this method and
            // assumes that the content id and size will be set
            PopulateContentIdAndSize(entity, store);

            while (true)
            {
                var batch = currentBatch;
                if (!batch.TryAdd(store, entity, onAdded))
                {
                    if (batch.TryReserveExecute())
                    {
                        currentBatch = new ElasticSearchBatch();
                        await store.Store.Service.UseClient(batch.ExecuteAsync);
                        return None.Value;
                    }
                }
            }
        }

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity)
            where T : class, ISearchEntity
        {
            var backgroundTask = AddAsync(store, entity);
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

            await Placeholder.NotImplementedAsync("Flush stored filters. Just make them a background task");

            await Placeholder.NotImplementedAsync("Wait for background operations to complete and flush again");
        }
    }
}
