using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public static class StoreExtensions
    {
        public static Task UpdateStoredFiltersAsync(this ElasticSearchEntityStore<IStoredFilter> storedFilterStore, IReadOnlyList<IStoredFilter> storedFilters)
        {
            return storedFilterStore.StoreAsync<StoredFilter>(storedFilters, UpdateMergeStoredFilter);
        }

        public static IStoredFilter UpdateMergeStoredFilter(IStoredFilter oldValue, IStoredFilter newValue)
        {
            var updatedStoredFilter = new StoredFilter(newValue);
            updatedStoredFilter.Uid = oldValue.Uid;

            // The filter will be unioned with values for StableIds and UnionFilters fields
            Contract.Assert(updatedStoredFilter.Filter == null);
            updatedStoredFilter.Filter = oldValue.Filter;

            return updatedStoredFilter;
        }
    }
}
