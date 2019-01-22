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
    internal partial class ElasticSearchStoredFilterBuilder
    {
        public string IndexName { get; set; }
        public ElasticSearchStore Store => EntityStore.Store;
        public readonly ElasticSearchEntityStore EntityStore;
        public readonly string BaseFilterName;

        private const int BatchSize = 2000;

        private readonly ConcurrentRoaringFilterBuilder StableIdBuildState;
        private readonly string[] unionFilterNames;

        public ElasticSearchStoredFilterBuilder(ElasticSearchEntityStore entityStore, string filterName, params string[] unionFilterNames)
        {
            EntityStore = entityStore;
            BaseFilterName = filterName;
            this.unionFilterNames = unionFilterNames;
            IndexName = entityStore.IndexName;

            StableIdBuildState = new ConcurrentRoaringFilterBuilder();
        }

        public void Add(ElasticEntityRef entityRef)
        {
            StableIdBuildState.Add(entityRef.StableId);
        }

        private async Task UpdateFilters(params StoredFilter[] filters)
        {
            await Store.StoredFilterStore.UpdateStoredFiltersAsync(filters);
        }

        public Task<StoredFilter> FinalizeAsync()
        {
            var filter = new StoredFilter()
            {
                DateUpdated = DateTime.UtcNow,
                Name = BaseFilterName,
                IndexName = IndexName,
            };

            StableIdBuildState.Complete();
            filter.ApplyStableIds(StableIdBuildState.RoaringFilter);

            filter.PopulateContentIdAndSize();

            return Task.FromResult(filter);
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
