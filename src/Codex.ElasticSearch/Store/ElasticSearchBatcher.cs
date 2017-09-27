using System;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatcher
    {
        // Commit stored filters (one per entity type)
        // Declared definition stored filter
        // Referenced definition stored filter
        // Repository stored filter (just OR the commit stored filter with current repository stored filter at the end)
        // Cumulative commit stored filter (just OR the commit stored filter with current commit stored filter at the end)
        public ElasticSearchStoredFilterBuilder[] StoredFiltersBuilders = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count + 10];

        public Task AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded = null)
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
}
