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

namespace Codex.ElasticSearch
{
    internal class ElasticSearchStoredFilterBuilder
    {
        public string IndexName => EntityStore.IndexName;
        public ElasticSearchStore Store => EntityStore.Store;
        public readonly ElasticSearchEntityStore EntityStore;
        public readonly string FilterName;
        public readonly string IntermediateFilterSuffix;

        private readonly ShardState[] ShardStates;

        public ElasticSearchStoredFilterBuilder(ElasticSearchEntityStore entityStore, string filterName)
        {
            EntityStore = entityStore;
            FilterName = filterName;

            IntermediateFilterSuffix = Guid.NewGuid().ToString();

            ShardStates = Enumerable.Range(0, entityStore.ShardCount).Select(shard => CreateShardState(shard)).ToArray();
        }

        private ShardState CreateShardState(int shard)
        {
            var shardState = new ShardState()
            {
                Shard = shard,
                ShardFilterUid = $"{IndexName}#{shard}|{FilterName}",
                ShardIntermediateFilterUid = $"{IndexName}#{shard}|{FilterName}|{IntermediateFilterSuffix}"
            };

            return shardState;
        }

        public void Add(ElasticClause clause)
        {
            var shard = clause.Shard;
            var shardState = ShardStates[shard];
            IReadOnlyList<ElasticClause> batch;
            if (shardState.Queue.AddAndTryGetBatch(clause, out batch))
            {
                Store.Service.UseClientBackground(async context =>
                {
                    var client = context.Client;
                    using (await shardState.Mutex.AcquireAsync())
                    {
                        // Refresh the entity and stored filter stores as the filter may
                        // query entities or prior stored filter
                        await client.RefreshAsync(IndexName).ThrowOnFailure();
                        await client.RefreshAsync(Store.StoredFilterStore.IndexName).ThrowOnFailure();

                        QueryContainerDescriptor<T> filterDescriptor = new QueryContainerDescriptor<T>();
                        QueryContainer filter = filterDescriptor;

                        AddPriorStoredFilterIncludeClause(shardState, filterDescriptor, ref filter);

                        AddBatchClauses(batch, filterDescriptor, ref filter);

                        Placeholder.Todo("Add date updated to stored filter to allow garbage collection/TTL");
                        await Store.StoredFilterStore.StoreAsync(new[]
                        {
                            new StoredFilter()
                            {
                                // TODO: We need to ensure only one thread/process is operating on a given stored filter
                                // at a time. Otherwise, its possible for them to stomp over each other. Maybe have a
                                // intermediate stored filter which is used to build up the value and when finalized it
                                // will be set to the stored filter under the UID.
                                // NOTE!!!! Intermediate stored filter should then be deleted
                                Uid = shardState.ShardIntermediateFilterUid,
                                IndexName = IndexName,
                                Shard = shard,
                                Filter = filter
                            }
                        });
                    }
                });
            }
        }

        private void AddBatchClauses(IReadOnlyList<ElasticClause> batch, QueryContainerDescriptor<T> filterDescriptor, ref QueryContainer filter)
        {
            throw new NotImplementedException();
        }

        private void AddPriorStoredFilterIncludeClause(ShardState shardState, QueryContainerDescriptor<T> filterDescriptor, ref QueryContainer filter)
        {
            throw new NotImplementedException();
        }

        public Task FlushAsync()
        {
            throw new NotImplementedException();
        }

        public async Task FinalizeAsync()
        {
            await Placeholder.NotImplementedAsync("Create new final stored filter from intermediate stored filters");

            await Placeholder.NotImplementedAsync("Delete intermediate stored filters for shards");
        }

        private class ShardState
        {
            public int Shard;
            public BatchQueue<ElasticClause> Queue;
            public SemaphoreSlim Mutex = TaskUtilities.CreateMutex();
            public string ShardIntermediateFilterUid { get; set; }
            public string ShardFilterUid { get; set; }
        }
    }

    public class ElasticClause
    {
        public int Shard;
    }

    public class ElasticIdentity : ElasticClause
    {
        public string Id;
    }
}
