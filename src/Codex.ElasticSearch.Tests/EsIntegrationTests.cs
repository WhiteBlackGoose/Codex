using Codex.ObjectModel;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class EsIntegrationTests
    {
        [Test]
        public async Task StoredFilterTest()
        {
            const int valuesToAdd = 1000;

            var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ShardCount = 1,
                Prefix = "estest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            await store.Clear();

            await store.InitializeAsync();

            Random random = new Random(12);

            HashSet<long> values = new HashSet<long>();
            HashSet<long> valuesToStore = new HashSet<long>();

            for (int i = 0; i < valuesToAdd; i++)
            {
                valuesToStore.Add(random.Next(0, valuesToAdd * 100));
            }

            // Store initial filter
            var filter1 = await StoreAndVerifyFilter(store, values, valuesToStore);

            // Verify that adding same values does not change filter
            var filter2 = await StoreAndVerifyFilter(store, values, valuesToStore);

            Assert.AreEqual(filter1.FilterHash, filter2.FilterHash);

            Assert.True(filter1.Filter.SequenceEqual(filter2.Filter), "Filter should be the same if unioned with same values");

            for (int i = 0; i < valuesToAdd; i++)
            {
                valuesToStore.Add(random.Next(valuesToAdd * 100, valuesToAdd * 200));
            }

            // Verify that adding different values does change filter
            var filter3 = await StoreAndVerifyFilter(store, values, valuesToStore);

            Assert.AreNotEqual(filter1.FilterHash, filter3.FilterHash);
            Assert.False(filter1.Filter.SequenceEqual(filter3.Filter), "Filter should be the same if unioned with same values");
        }

        private async Task<IStoredFilter> StoreAndVerifyFilter(ElasticSearchStore store, HashSet<long> values, HashSet<long> valuesToStore, [CallerLineNumber] int line = 0)
        {
            values.UnionWith(valuesToStore);

            await store.RegisteredEntityStore.StoreAsync(
                valuesToStore.Select(stableId =>
                {
                    return new RegisteredEntity()
                    {
                        Uid = GetUidFromStableId(stableId),
                        DateAdded = DateTime.UtcNow,
                        ShardStableId = stableId,
                    };
                }).ToArray());

            await store.RegisteredEntityStore.RefreshAsync();

            string storedFilterId = "TEST_STORED_FILTER1";
            await store.StoredFilterStore.UpdateStoredFiltersAsync(new[]
            {
                new StoredFilter()
                {
                    Uid = storedFilterId,
                    StableIds = valuesToStore.ToList(),
                    FilterHash = string.Empty,
                }
            });

            var retrievedFilterResponse = await store.StoredFilterStore.GetAsync(storedFilterId);
            var retrievedFilter = retrievedFilterResponse.Result;

            Assert.AreEqual(values.Count, retrievedFilter.FilterCount, $"Caller Line: {line}");
            Assert.AreNotEqual(string.Empty, retrievedFilter.FilterHash, $"Caller Line: {line}");

            return retrievedFilter;
        }

        private string GetUidFromStableId(long stableId)
        {
            return Convert.ToString(stableId, 16);
        }
    }
}
