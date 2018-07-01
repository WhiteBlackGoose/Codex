using Codex.ObjectModel;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
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
        private const int ByteBitCount = 8;
        public const int StableIdGroupMaxValue = byte.MaxValue;

        public static Task UpdateStoredFiltersAsync(this ElasticSearchEntityStore<IStoredFilter> storedFilterStore, IReadOnlyList<IStoredFilter> storedFilters)
        {
            return storedFilterStore.StoreAsync<StoredFilter>(storedFilters, updateMergeFunction: null);
        }

        public static string GetFilterId(string baseFilterId, string indexName)
        {
            return $"{indexName}|{baseFilterId}";
        }

        public static int ExtractStableId(long version)
        {
            version--;
            return (int)(version >> ByteBitCount);
        }

        public static long ComputeVersion(int stableIdGroup, int stableId)
        {
            Contract.Assert(stableIdGroup >= 0 && stableIdGroup < StableIdGroupMaxValue);
            long version =  (byte)stableIdGroup | (((long)stableId) << ByteBitCount);
            return version + 1;
        }

        public static int GetStableIdGroup(this ISearchEntity entity)
        {
            return IndexingUtilities.ComputeFullHash(entity.RoutingKey ?? entity.Uid ?? entity.EntityContentId).GetByte(0) % StableIdGroupMaxValue;
        }

        public static string GetRoutingSuffix(this ISearchEntity entity)
        {
            if (entity.RoutingKey == null) return string.Empty;
            return $"#{GetStableIdGroup(entity)}";
        }

        public static string GetRouting(string uid)
        {
            if (!uid.Contains("#"))
            {
                return null;
            }
            else
            {
                return uid.Substring(uid.LastIndexOf('#'));
            }

            //Placeholder.Todo("Routing should be based something other than uid OR uid needs to incorporate other aspects.");
            //// TODO: For instance, files should be routed based on file name to increase likelihood of deduplication. This
            //// seems to have a lot of overlap with index sorting. Also, it would nice of uid was fairly short, so the components should
            //// probably be hashed
            //// TODO: Consider having routing value embedded in uid (i.e. {contentid}#{routing})

            //return IndexingUtilities.ComputeFullHash(uid).GetByte(0);
        }

        public static Task<ElasticSearchResponse<IReadOnlyList<T>>> GetStoredFilterEntities<T>(this ElasticSearchEntityStore<T> entityStore, string baseFilterId, int maxCount = 10)
            where T : class, ISearchEntity
        {
            throw Placeholder.NotImplementedException();

        //    return entityStore.Store.Service.UseClient<IReadOnlyList<T>>(async context =>
        //    {
        //        var client = context.Client;

        //        var result = await client.SearchAsync<T>(
        //            s => s.Query(f => f.Bool(bq => bq.Filter(qcd => qcd.StoredFilter(
        //                sfq => sfq.Field(e => e.ShardStableId).FilterLookup<IStoredFilter>(fl => fl
        //                    .Id(GetTokenizedFilterId(baseFilterId, entityStore.IndexName))
        //                    .Index(entityStore.Store.StoredFilterStore.IndexName)
        //                    .Path(sf => sf.Filter))))))
        //            .Index(entityStore.IndexName)
        //            .Take(maxCount))
        //            .ThrowOnFailure();

        //        return result.Hits.Select(h => h.Source).ToList();

        //        //var response = await context.Client
        //        //    .GetAsync<T>(uid, g => g.Index(IndexName))
        //        //    .ThrowOnFailure();

        //        //return response.Source;
        //    });
        }
    }
}
