﻿using Codex.ElasticSearch.Utilities;
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
    public class ElasticSearchStoredFilterBuilder<T>
        where T : class
    {
        public string IndexName;
        public ElasticSearchStore Store;
        public ElasticSearchEntityStore<T> EntityStore;
        public string FilterName;

        private ShardState[] ShardStates;

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

                        await Store.StoredFilterStore.StoreAsync(new[]
                        {
                            new StoredFilter()
                            {
                                Uid = $"{IndexName}#{shard}|{FilterName}",
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

        private class ShardState
        {
            public int Shard;
            public BatchQueue<ElasticClause> Queue;
            public SemaphoreSlim Mutex = TaskUtilities.CreateMutex();
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
