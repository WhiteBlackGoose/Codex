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
using Codex.Serialization;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchStoredFilterBuilder
    {
        public string IndexName { get; set; }
        public ElasticSearchStore Store => EntityStore.Store;
        public readonly ElasticSearchEntityStore EntityStore;
        public readonly string BaseFilterName;

        private const int BatchSize = 2000;

        private readonly StableIdBuild StableIdBuildState;
        private readonly string[] unionFilterNames;

        public ElasticSearchStoredFilterBuilder(ElasticSearchEntityStore entityStore, string filterName, params string[] unionFilterNames)
        {
            EntityStore = entityStore;
            BaseFilterName = filterName;
            this.unionFilterNames = unionFilterNames;
            IndexName = entityStore.IndexName;

            StableIdBuildState = CreateStableIdBuild();
        }

        private StableIdBuild CreateStableIdBuild()
        {
            var shardState = new StableIdBuild()
            {
                Queue = new BatchQueue<int>(BatchSize)
            };

            return shardState;
        }

        public void Add(ElasticEntityRef entityRef)
        {
            StableIdBuildState.Add(entityRef);
        }

        private async Task UpdateFilters(params StoredFilter[] filters)
        {
            await Store.StoredFilterStore.UpdateStoredFiltersAsync(filters);
        }

        public Task<StoredFilter> FinalizeAsync()
        {
            //await RefreshStoredFilterIndex();

            var filter = new StoredFilter()
            {
                DateUpdated = DateTime.UtcNow,
                Name = BaseFilterName,
                IndexName = IndexName,
            };

            StableIdBuildState.Complete();
            filter.ApplyStableIds(StableIdBuildState.RoaringFilter);

            filter.PopulateContentIdAndSize();

            //await UpdateFilters(filter);

            return Task.FromResult(filter);

            //await RefreshStoredFilterIndex();
        }

        private class StableIdBuild
        {
            public BatchQueue<int> Queue;
            private object mutex = new object();
            public RoaringDocIdSet RoaringFilter { get; set; } = RoaringDocIdSet.Empty;

            //private ConcurrentDictionary<int, ElasticEntityRef> dedupMap = new ConcurrentDictionary<int, ElasticEntityRef>();

            public void Add(ElasticEntityRef entity)
            {
                if (entity.StableId == -1)
                {
                    Debug.Fail($"{entity}");
                }
                //else if (!dedupMap.TryAdd(entity.StableId, entity))
                //{
                //    //Debug.Fail($"Conflict: [{entity}] | [{dedupMap[entity.StableId]}");
                //}

                if (Queue.AddAndTryGetBatch(entity.StableId, out var batch))
                {
                    AddIds(batch);
                }
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
                lock (mutex)
                {
                    batch.Sort();
                    var filterBuilder = new RoaringDocIdSet.Builder();

                    IEnumerable<int> ids = batch.SortedUnique(Comparer<int>.Default);

                    if (RoaringFilter.Count != 0)
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
    }

    public struct ElasticEntityRef
    {
        public string Uid;
        public int StableId;

        public ElasticEntityRef(ISearchEntity entity)
        {
            Uid = entity.Uid;
            StableId = entity.StableId;
        }

        public override string ToString()
        {
            return $"{Uid}:{StableId}";
        }
    }
}
