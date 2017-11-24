using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using System.Diagnostics.Contracts;
using Codex.Sdk.Utilities;
using Codex.ElasticSearch.Utilities;
using Codex.Storage.ElasticProviders;
using Codex.ObjectModel;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatch
    {
        public readonly BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public readonly BulkDescriptor RegistryBulkDescriptor = new BulkDescriptor();

        public readonly List<Item> EntityItems = new List<Item>();

        private readonly AtomicBool canReserveExecute = new AtomicBool();

        private int CurrentSize = 0;

        private readonly ElasticSearchBatcher batcher;

        public ElasticSearchBatch(ElasticSearchBatcher batcher)
        {
            this.batcher = batcher;
        }

        public bool TryReserveExecute()
        {
            return canReserveExecute.TrySet(value: true);
        }

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            lock(this)
            {
                // Prevent any subsequent additions by calling this in the lock.
                // Generally, TryReserveExecute would already be called except
                // for final batch which may not reach capacity before being flushed
                TryReserveExecute();
            }

            var registerResponse = await context.Client.BulkAsync(RegistryBulkDescriptor.CaptureRequest(context)).ThrowOnFailure();
            Contract.Assert(EntityItems.Count == registerResponse.Items.Count);

            int batchIndex = 0;
            foreach (var registerResponseItem in registerResponse.Items)
            {
                var item = EntityItems[batchIndex];
                batchIndex++;

                // Use the sequence number of the registered item as the stable id
                item.Entity.ShardStableId = registerResponseItem.SequenceNumber;
            }

            var response = await context.Client.BulkAsync(BulkDescriptor.CaptureRequest(context)).ThrowOnFailure();
            Contract.Assert(EntityItems.Count == response.Items.Count);

            batchIndex = 0;
            foreach (var responseItem in response.Items)
            {
                var item = EntityItems[batchIndex];
                batchIndex++;

                if (Placeholder.MissingFeature("Need to implement stored filter support"))
                {
                    var entityRef = new ElasticEntityRef(
                            shard: GetShard(responseItem),
                            stableId: item.Entity.ShardStableId);
                    batcher.CommitSearchTypeStoredFilters[item.SearchType.Id].Add(entityRef);
                    foreach (var additionalStoredFilter in item.AdditionalStoredFilters)
                    {
                        additionalStoredFilter.Add(entityRef);
                    }
                }

                if (IsAdded(responseItem))
                {
                    item.OnAdded?.Invoke();
                }
            }

            return response;
        }

        private bool IsAdded(BulkResponseItemBase item)
        {
            return (item as BulkCreateResponseItem).Created;
        }

        private int GetShard(BulkResponseItemBase item)
        {
            return item.Shard;
        }

        public bool TryAdd<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded, ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            if (!CanFit(entity))
            {
                return false;
            }

            lock (this)
            {
                if (!CanFit(entity))
                {
                    return false;
                }

                CurrentSize += entity.EntityContentSize;

                var item = new Item()
                {
                    BatchIndex = EntityItems.Count,
                    Entity = entity,
                    EntityStore = store,
                    OnAdded = onAdded,
                    AdditionalStoredFilters = additionalStoredFilters
                };

                EntityItems.Add(item);

                store.AddRegisterOperation(RegistryBulkDescriptor, new RegisteredEntity(item.Entity)
                {
                    DateAdded = DateTime.UtcNow
                });

                store.AddIndexOperation(BulkDescriptor, entity);
                return true;
            }
        }

        private bool CanFit<T>(T entity) where T : class, ISearchEntity
        {
            var size = CurrentSize;
            if (size != 0 && size + entity.EntityContentSize > ElasticConstants.BatchSizeBytes)
            {
                return false;
            }

            if (canReserveExecute.Value)
            {
                // Already reserved for execution
                return false;
            }

            return true;
        }

        public class Item
        {
            public int BatchIndex { get; set; }
            public ISearchEntity Entity { get; set; }
            public SearchType SearchType => EntityStore.SearchType;
            public ElasticSearchEntityStore EntityStore { get; set; }
            public Action OnAdded { get; set; }
            public ElasticSearchStoredFilterBuilder[] AdditionalStoredFilters { get; set; }
        }
    }
}
