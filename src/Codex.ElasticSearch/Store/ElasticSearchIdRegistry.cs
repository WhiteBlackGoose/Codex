using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public class ElasticSearchIdRegistry : IStableIdRegistry
    {
        private readonly ElasticSearchStore store;
        private readonly ElasticSearchEntityStore<IStableIdMarker> idStore;
        private readonly ElasticSearchService service;

        public ElasticSearchIdRegistry(ElasticSearchStore store)
        {
            this.store = store;
            this.service = store.Service;
            this.idStore = store.StableIdMarkerStore;
        }

        public Task SetStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            // For each item, reserve ids from the {IndexName}:{StableIdGroup} StableIdMarker document and assign tentative ids for each
            // item
            // Next attempt to get or add a document with Uid = {IndexName}.{Uid}, StableIdGroup, StableId
            // Assign the StableId of the final document after the get or add to the item

            throw new NotImplementedException();
        }
    }
}
