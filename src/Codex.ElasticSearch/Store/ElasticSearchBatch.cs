using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using System.Diagnostics.Contracts;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatch
    {
        public BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public List<Item> Items = new List<Item>();

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            var response = await context.Client.BulkAsync(BulkDescriptor);
            Contract.Assert(Items.Count == response.Items.Count);

            int batchIndex = 0;
            foreach (var item in response.Items)
            {
                if (IsAdded(item))
                {
                    Items[batchIndex].OnAdded?.Invoke();
                }
            }

            return response;
        }

        private bool IsAdded(BulkResponseItemBase item)
        {
            throw Placeholder.NotImplemented("Check if item was added or not");
        }

        public class Item
        {
            public int BatchIndex { get; set; }
            public ISearchEntity Entity { get; set; }
            public ElasticSearchEntityStore EntityStore { get; set; }
            public Action OnAdded { get; set; }
            public int[] AdditionalStoredFilterBuilderIds { get; set; }
        }

        public bool TryAdd<T>(ElasticSearchEntityStore<T> store, T entity, Action onAdded = null)
            where T : class, ISearchEntity
        {
            lock (this)
            {
                Placeholder.Todo("Ensure batch size");

                var item = new Item()
                {
                    BatchIndex = Items.Count,
                    Entity = entity,
                    EntityStore = store,
                    OnAdded = onAdded,
                    AdditionalStoredFilterBuilderIds = Placeholder.Value<int[]>("Populate")
                };

                Items.Add(item);

                store.AddCreateOperation(BulkDescriptor, entity);
                return true;
            }
        }
    }
}
