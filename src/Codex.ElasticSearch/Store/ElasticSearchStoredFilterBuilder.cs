using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Codex.Utilities;
using System.Diagnostics.Contracts;
using Codex.ObjectModel;

using static Codex.ElasticSearch.StoredFilterUtilities;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchStoredFilterBuilder
    {
        public string IndexName => EntityStore.IndexName;
        public ElasticSearchStore Store => EntityStore.Store;
        public readonly ElasticSearchEntityStore EntityStore;
        public readonly string FilterName;
        public readonly string IntermediateFilterSuffix;

        private const int BatchSize = 10000;

        private readonly StableIdGroupBuild[] ShardStates;
        private ConcurrentQueue<Task> storedFilterUpdateTasks = new ConcurrentQueue<Task>();
        private readonly string[] unionFilterNames;

        public ElasticSearchStoredFilterBuilder(ElasticSearchEntityStore entityStore, string filterName, params string[] unionFilterNames)
        {
            EntityStore = entityStore;
            FilterName = filterName;
            this.unionFilterNames = unionFilterNames;

            IntermediateFilterSuffix = Guid.NewGuid().ToString();

            ShardStates = Enumerable.Range(0, StableIdGroupMaxValue).Select(stableIdGroupNumber => CreateShardState(stableIdGroupNumber)).ToArray();
        }

        private StableIdGroupBuild CreateShardState(int shard)
        {
            var shardFilterId = GetFilterId(FilterName, IndexName, shard);
            var shardState = new StableIdGroupBuild()
            {
                IndexName = IndexName,
                Shard = shard,
                ShardFilterUid = shardFilterId,
                ShardIntermediateFilterUid = $"{shardFilterId}[{IntermediateFilterSuffix}]",
                UnionFilterUids = unionFilterNames.Select(name => GetFilterId(name, IndexName, shard)).ToArray(),
                Queue = new BatchQueue<ElasticEntityRef>(BatchSize)
            };

            shardState.Filter = shardState.CreateStoredFilter(shardState.ShardIntermediateFilterUid);
            return shardState;
        }

        public void Add(ElasticEntityRef entityRef)
        {
            var shardState = ShardStates[entityRef.StableIdGroup];
            IReadOnlyList<ElasticEntityRef> batch;
            if (shardState.Queue.AddAndTryGetBatch(entityRef, out batch))
            {
                storedFilterUpdateTasks.Enqueue(Store.Service.UseClient(async context =>
                {
                    var client = context.Client;
                    using (await shardState.Mutex.AcquireAsync())
                    {
                        // Only refresh if there are changes
                        if (shardState.LastQueueTotalCount != shardState.Queue.TotalCount)
                        {
                            // Refresh the entity and stored filter stores as the filter may
                            // query entities or prior stored filter
                            await client.RefreshAsync(IndexName).ThrowOnFailure();

                            shardState.LastQueueTotalCount = shardState.Queue.TotalCount;
                        }

                        await RefreshStoredFilterIndex();

                        var filter = shardState.Filter;
                        filter.DateUpdated = DateTime.UtcNow;

                        filter.StableIds.Clear();
                        foreach (var batchEntityRef in batch)
                        {
                            filter.StableIds.Add(batchEntityRef.StableId);
                        }

                        if (Placeholder.MissingFeature("Proper handling of stored filter replacement"))
                        {
                            await UpdateFilters(filter);
                        }

                        return None.Value;
                    }
                }));
            }
        }

        private async Task UpdateFilters(params StoredFilter[] filters)
        {
            Placeholder.NotImplemented("Proper handling of stored filter replacement. Need to clear when storing initial values");
            await Store.StoredFilterStore.UpdateStoredFiltersAsync(filters);
        }

        public async Task FlushAsync()
        {
            foreach (var task in storedFilterUpdateTasks)
            {
                await task;
            }
        }

        public async Task FinalizeAsync()
        {
            await FlushAsync();

            await RefreshStoredFilterIndex();

            var filters = ShardStates.SelectMany(shardState =>
            {
                var filter = shardState.Filter;

                // Set Uid to final value
                filter.Uid = shardState.ShardFilterUid;

                return new[] { filter }.Concat(shardState.UnionFilterUids.Select(unionFilterUid =>
                {
                    return shardState.CreateStoredFilter(unionFilterUid, shardState.ShardIntermediateFilterUid);
                }));
            }).ToArray();

            await UpdateFilters(filters);

            await RefreshStoredFilterIndex();

            await EntityStore.DeleteAsync(ShardStates.Select(ss => ss.ShardIntermediateFilterUid));
        }

        private async Task RefreshStoredFilterIndex()
        {
            await Store.Service.UseClient(async context =>
            {
                return await context.Client.RefreshAsync(Store.StoredFilterStore.IndexName).ThrowOnFailure();
            });
        }

        private class StableIdGroupBuild
        {
            public int Shard;
            public string IndexName;
            public BatchQueue<ElasticEntityRef> Queue;
            public SemaphoreSlim Mutex = TaskUtilities.CreateMutex();
            public StoredFilter Filter { get; set; }
            public string[] UnionFilterUids { get; set; }
            public string ShardIntermediateFilterUid { get; set; }
            public string ShardFilterUid { get; set; }
            public int LastQueueTotalCount;

            public StoredFilter CreateStoredFilter(string filterUid, params string[] additionalBaseUids)
            {
                var filter = new StoredFilter()
                {
                    Uid = filterUid,
                    IndexName = IndexName,
                    Shard = Shard,
                };

                filter.BaseUids.Add(filterUid);
                filter.BaseUids.AddRange(additionalBaseUids);

                return filter;
            }
        }
    }

    public struct ElasticEntityRef
    {
        public int StableIdGroup;
        public int StableId;

        public ElasticEntityRef(int stableIdGroup, int stableId)
        {
            StableIdGroup = stableIdGroup;
            StableId = stableId;
        }
    }
}
