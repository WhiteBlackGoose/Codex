using Codex.ElasticSearch.Formats;
using Codex.ElasticSearch.Search;
using Codex.ElasticSearch.Utilities;
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
        public const long MaxVersion = 1L << 40;

        private const string CACHED_STORED_FILTER_ID_SPECIFIER = ",#=";

        public static Task UpdateStoredFiltersAsync(this ElasticSearchEntityStore<IStoredFilter> storedFilterStore, IReadOnlyList<IStoredFilter> storedFilters)
        {
            return storedFilterStore.StoreAsync<StoredFilter>(storedFilters, updateMergeFunction: null, replace: true);
        }

        public static SearchDescriptor<T> StoredFilterQuery<T>(this SearchDescriptor<T> searchDescriptor, StoredFilterSearchContext context, string indexName, Func<QueryContainerDescriptor<T>, QueryContainer> query, string filterIndexName = null)
            where T : class, ISearchEntity
        {
            var storedFilterUid = GetFilterName(context.StoredFilterUidPrefix, filterIndexName ?? indexName);
            return searchDescriptor
                .Query(q => q.Bool(bq => bq
                    .Must(query)
                    .Filter(q1 => q1.Terms(tq => tq.Field(e => e.StableId)
                        // Specify the id under which the stored filter is cached. This is unique id of the stored filter
                        // i.e. repos/allsources/{ingestId}/{indexName}
                        .Name(CACHED_STORED_FILTER_ID_SPECIFIER + storedFilterUid)
                        .TermsLookup<IStoredFilter>(ld => ld
                            .Index(context.StoredFilterIndexName)
                            // Specifies the id of the stored filter to lookup. This is typically the alias
                            // i.e. repos/allsources/{ingestId}/{indexName}
                            .Id(storedFilterUid)
                            .Path(sf => sf.StableIds))))))
                .Index(indexName)
                .CaptureRequest(context);
        }

        public static string GetDeclaredDefinitionsIndexName(string baseIndexName)
        {
            return $"{baseIndexName}.declared";
        }

        /// <summary>
        /// {indexName}/repos/{repositoryId}
        /// </summary>
        public static string GetIndexRepositoryFilterUid(string baseId, string indexName)
        {
            return GetFilterName(baseId, indexName: indexName);
        }

        public static string GetStoredFilterAliasUid(string repositoryName)
        {
            return $"aliases/{GetRepositoryBaseFilterName(repositoryName)}";
        }

        public static string GetRepositoryBaseFilterName(string repositoryName)
        {
            return $"repos/{repositoryName}";
        }

        public static string GetFilterName(string baseFilterId, string indexName)
        {
            return $"{baseFilterId}:{indexName}";
        }

        public static int ExtractStableId(long version)
        {
            return (int)(MaxVersion - version);
        }

        public static long ComputeVersion(int stableId)
        {
            // Version needs to be decrease in order to keep documents from being replaced
            return MaxVersion - stableId;
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

        public static IEnumerable<int> GetStableIdValues(this IStoredFilter filter)
        {
            return RoaringDocIdSet.FromBytes(filter.StableIds).Enumerate();
        }

        public static StoredFilter ApplyStableIds(this StoredFilter filter, IEnumerable<int> stableIds)
        {
            var filterBuilder = new RoaringDocIdSet.Builder();

            foreach (var id in stableIds)
            {
                filterBuilder.Add(id);
            }

            var stableIdSet = filterBuilder.Build();
            return ApplyStableIds(filter, stableIdSet);
        }

        public static StoredFilter ApplyStableIds(this StoredFilter filter, RoaringDocIdSet stableIdSet)
        {
            filter.StableIds = stableIdSet.GetBytes();
            filter.FilterHash = new Murmur3().ComputeHash(filter.StableIds).ToBase64String();
            filter.Cardinality = stableIdSet.Cardinality();

            return filter;
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
            //throw Placeholder.NotImplementedException();

            return entityStore.Store.Service.UseClient<IReadOnlyList<T>>(async context =>
            {
                var client = context.Client;

                var result = await client.SearchAsync<T>(
                    s => s.Query(f => f.Bool(bq => bq.Filter(qcd => qcd.Terms(
                        tsd => tsd.Field(e => e.StableId)
                        .TermsLookup<IStoredFilter>(ld => ld
                            .Index(entityStore.Store.StoredFilterStore.IndexName)
                            .Id(baseFilterId)
                            .Path(sf => sf.StableIds))))))
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
