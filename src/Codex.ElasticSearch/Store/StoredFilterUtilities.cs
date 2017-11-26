using Codex.ObjectModel;
using Codex.Storage.ElasticProviders;
using Nest;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public static class StoredFilterUtilities
    {
        public static Task UpdateStoredFiltersAsync(this ElasticSearchEntityStore<IStoredFilter> storedFilterStore, IReadOnlyList<IStoredFilter> storedFilters)
        {
            return storedFilterStore.StoreAsync<StoredFilter>(storedFilters, UpdateMergeStoredFilter);
        }

        public static IStoredFilter UpdateMergeStoredFilter(IStoredFilter oldValue, IStoredFilter newValue)
        {
            var updatedStoredFilter = new StoredFilter(newValue);
            updatedStoredFilter.Uid = oldValue.Uid;

            // The filter will be unioned with values for StableIds and UnionFilters fields
            Contract.Assert(updatedStoredFilter.Filter == null);
            updatedStoredFilter.Filter = oldValue.Filter;

            return updatedStoredFilter;
        }

        public static string GetTokenizedFilterId(string baseFilterId, string indexName)
        {
            return GetFilterId(baseFilterId, indexName, StoredFilterQuery.ShardIdToken);
        }

        public static string GetFilterId(string baseFilterId, string indexName, object shard)
        {
            return $"{indexName}#{shard}|{baseFilterId}";
        }

        public static Task<ElasticSearchResponse<IReadOnlyList<T>>> GetStoredFilterEntities<T>(this ElasticSearchEntityStore<T> entityStore, string baseFilterId, int maxCount = 10)
            where T : class, ISearchEntity
        {
            return entityStore.Store.Service.UseClient<IReadOnlyList<T>>(async context =>
            {
                var client = context.Client;

                var result = await client.SearchAsync<T>(
                    s => s.Query(f => f.Bool(bq => bq.Filter(qcd => qcd.StoredFilter(
                        sfq => sfq.Field(e => e.ShardStableId).FilterLookup<IStoredFilter>(fl => fl
                            .Id(GetTokenizedFilterId(baseFilterId, entityStore.IndexName))
                            .Index(entityStore.Store.StoredFilterStore.IndexName)
                            .Path(sf => sf.Filter))))))
                    .Index(entityStore.IndexName)
                    .Take(maxCount))
                    .ThrowOnFailure();

                return result.Hits.Select(h => h.Source).ToList();

                //var response = await context.Client
                //    .GetAsync<T>(uid, g => g.Index(IndexName))
                //    .ThrowOnFailure();

                //return response.Source;
            });
        }
    }
}
