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
using Codex.ElasticSearch.Formats;
using System.Diagnostics;

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

        private StableIdGroupBuild CreateShardState(int group)
        {
            var shardState = new StableIdGroupBuild()
            {
                IndexName = IndexName,
                Group = group,
                Queue = new BatchQueue<int>(BatchSize)
            };

            return shardState;
        }

        public void Add(ElasticEntityRef entityRef)
        {
            var shardState = ShardStates[entityRef.StableIdGroup];
            if (shardState.AddAndTryGetBatch(entityRef, out var batch))
            {
                storedFilterUpdateTasks.Enqueue(Store.Service.UseClient(async context =>
                {
                    var client = context.Client;
                    using (await shardState.Mutex.AcquireAsync())
                    {
                        shardState.AddIds(batch);

                        return None.Value;
                    }
                }));
            }
        }

        private async Task UpdateFilters(params StoredFilter[] filters)
        {
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

            var filter = new StoredFilter()
            {
                DateUpdated = DateTime.UtcNow,
                Uid = GetFilterId(FilterName, IndexName),
                IndexName = IndexName,
                StableIds = new GroupedStoredFilterIds()
            };

            foreach (var shardState in ShardStates)
            {
                shardState.Complete();

                if (shardState.RoaringFilter != null)
                {
                    filter.StableIds[shardState.Group] = shardState.RoaringFilter.GetBytes();
                    filter.Cardinality += shardState.Queue.TotalCount;
                }
            }

            await UpdateFilters(filter);

            await RefreshStoredFilterIndex();
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
            public int Group;
            public string IndexName;
            public BatchQueue<int> Queue;
            public SemaphoreSlim Mutex = TaskUtilities.CreateMutex();
            public RoaringDocIdSet RoaringFilter { get; set; }

            //private ConcurrentDictionary<int, ElasticEntityRef> dedupMap = new ConcurrentDictionary<int, ElasticEntityRef>();

            public bool AddAndTryGetBatch(ElasticEntityRef entity, out List<int> batch)
            {
                if (entity.StableId == -1)
                {
                    Debug.Fail($"{entity}");
                }
                //else if (!dedupMap.TryAdd(entity.StableId, entity))
                //{
                //    //Debug.Fail($"Conflict: [{entity}] | [{dedupMap[entity.StableId]}");
                //}

                return Queue.AddAndTryGetBatch(entity.StableId, out batch);
            }

            public void Complete()
            {
                if (Queue.TryGetBatch(out var batch))
                {
                    AddIds(batch);
                }
            }

            public void AddIds(List<int> batch)
            {
                batch.Sort();
                var filterBuilder = new RoaringDocIdSet.Builder();

                IEnumerable<int> ids = batch.SortedUnique(Comparer<int>.Default);

                if (RoaringFilter != null)
                {
                    ids = RoaringFilter.Enumerate().ExclusiveInterleave(ids, Comparer<int>.Default);
                }

                foreach (var id in ids)
                {
                    filterBuilder.Add(id);
                }

                RoaringFilter = filterBuilder.Build();
            }
        }
    }

    public struct ElasticEntityRef
    {
        public string Uid;
        public int StableIdGroup;
        public int StableId;

        public ElasticEntityRef(ISearchEntity entity)
        {
            Uid = entity.Uid;
            StableIdGroup = entity.StableIdGroup;
            StableId = entity.StableId;
        }

        public override string ToString()
        {
            return $"{Uid}:{StableIdGroup}#{StableId}";
        }
    }
}
