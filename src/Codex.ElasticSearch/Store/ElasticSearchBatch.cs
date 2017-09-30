using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using System.Diagnostics.Contracts;
using Codex.Sdk.Utilities;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatch
    {
        public readonly BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public readonly List<Item> EntityItem = new List<Item>();

        private readonly AtomicBool canReserveExecute = new AtomicBool();

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
            var response = await context.Client.BulkAsync(BulkDescriptor);
            Contract.Assert(EntityItem.Count == response.Items.Count);

            int batchIndex = 0;
            foreach (var responseItem in response.Items)
            {
                var item = EntityItem[batchIndex];
                batcher.CommitSearchTypeStoredFilters[item.SearchType.Id].Add(
                    new ElasticEntityRef(
                        shard: GetShard(responseItem),
                        stableId: GetStableId(responseItem)));

                if (IsAdded(responseItem))
                {
                    item.OnAdded?.Invoke();
                }

                batchIndex++;
            }

            return response;
        }

        private bool IsAdded(BulkResponseItemBase item)
        {

            throw Placeholder.NotImplemented("Check if item was added or not");
        }

        private int GetShard(BulkResponseItemBase item)
        {
            throw Placeholder.NotImplemented("Need to add this in ElasticSearch and Nest");
        }

        private long GetStableId(BulkResponseItemBase item)
        {
            throw Placeholder.NotImplemented("Need to add this in ElasticSearch (derived from sequence number) and Nest");
        }

        public bool TryAdd<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded, ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            lock (this)
            {
                Placeholder.Todo("Ensure batch size");

                var item = new Item()
                {
                    BatchIndex = EntityItem.Count,
                    Entity = entity,
                    EntityStore = store,
                    OnAdded = onAdded,
                    AdditionalStoredFilters = additionalStoredFilters
                };

                EntityItem.Add(item);

                store.AddCreateOperation(BulkDescriptor, entity);
                return true;
            }
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
