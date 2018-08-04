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
using System.Net;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatch
    {
        public readonly BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public readonly List<Item> EntityItems = new List<Item>();

        public readonly List<Item> ReturnedStableIdItems = new List<Item>();

        private readonly AtomicBool canReserveExecute = new AtomicBool();

        private int CurrentSize = 0;

        private readonly ElasticSearchBatcher batcher;
        private readonly IStableIdRegistry stableIdRegistry;

        public ElasticSearchBatch(ElasticSearchBatcher batcher)
        {
            this.batcher = batcher;
            this.stableIdRegistry = batcher.StableIdRegistry;
        }

        public bool TryReserveExecute()
        {
            return canReserveExecute.TrySet(value: true);
        }

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            lock (this)
            {
                // Prevent any subsequent additions by calling this in the lock.
                // Generally, TryReserveExecute would already be called except
                // for final batch which may not reach capacity before being flushed
                TryReserveExecute();
            }

            //Task.Delay(1);
            //return null;

            // Reserve stable ids
            int batchIndex = 0;
            await stableIdRegistry.SetStableIdsAsync(EntityItems);

            var response = await context.Client.BulkAsync(BulkDescriptor.CaptureRequest(context)).ThrowOnFailure(allowInvalid: true);
            Contract.Assert(EntityItems.Count == response.Items.Count);

            batchIndex = 0;
            foreach (var responseItem in response.Items)
            {
                var item = EntityItems[batchIndex];
                batchIndex++;

                if (Placeholder.IsCommitModelEnabled)
                {
                    var entityRef = new ElasticEntityRef(item.Entity);
                    batcher.CommitSearchTypeStoredFilters[item.SearchType.Id].Add(entityRef);
                    foreach (var additionalStoredFilter in item.AdditionalStoredFilters)
                    {
                        additionalStoredFilter.Add(entityRef);
                    }
                }
            }

            return response;
        }

        private bool IsAdded(IBulkResponseItem item)
        {
            return item.Status == (int)HttpStatusCode.Created;
        }

        public bool TryAdd<T>(ElasticSearchEntityStore<T> store, T entity, ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
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

                var item = new Item(entity)
                {
                    BatchIndex = EntityItems.Count,
                    EntityStore = store,
                    AdditionalStoredFilters = additionalStoredFilters
                };

                EntityItems.Add(item);

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

        public class Item : IStableIdItem
        {
            public int? StableId { get; set; }
            public int BatchIndex { get; set; }
            public ISearchEntity Entity { get; }
            public SearchType SearchType => EntityStore.SearchType;
            public ElasticSearchEntityStore EntityStore { get; set; }
            public ElasticSearchStoredFilterBuilder[] AdditionalStoredFilters { get; set; }

            public int StableIdGroup => Entity.StableIdGroup;
            public string Uid => Entity.Uid;

            int IStableIdItem.StableId
            {
                get
                {
                    return StableId.Value;
                }
                set
                {
                    SetStableId(value);
                }
            }

            public bool IsAdded { get; set; }

            public Item(ISearchEntity entity)
            {
                Entity = entity;
            }

            public void SetStableId(int stableId)
            {
                StableId = stableId;
                Entity.StableId = stableId;
                Entity.EntityVersion = StoredFilterUtilities.ComputeVersion(StableIdGroup, stableId);
            }

            public override string ToString()
            {
                return $"SID: {StableId ?? -1}, Group: {StableIdGroup}";
            }
        }
    }
}
