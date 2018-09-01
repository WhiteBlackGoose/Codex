using Codex.ElasticSearch.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Utilities;
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
        public async Task TestDuplicate()
        {
            var originalStore = DirectoryCodexStoreTests.CreateInputStore();

            var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
            {
                ClearIndicesBeforeUse = true,
                CreateIndices = true,
                ShardCount = 1,
                Prefix = "estest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            await store.InitializeAsync();

            await originalStore.ReadAsync(store);

            await originalStore.ReadAsync(store);
        }

        [Test]
        public async Task ReservingStableIds()
        {
            var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ClearIndicesBeforeUse = true,
                ShardCount = 1,
                Prefix = "estest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            await store.InitializeAsync();

            var idRegistry = new ElasticSearchIdRegistry(store);

            int iterations = 3;
            string reservationId = null;
            for (int i = 0; i < iterations; i++)
            {
                var reservation = await idRegistry.ReserveIds(SearchTypes.BoundSource);
                reservationId = reservation.ReservationId;
                Assert.True(reservation.ReservedIds.SequenceEqual(Enumerable.Range(i * ElasticSearchIdRegistry.ReserveCount, ElasticSearchIdRegistry.ReserveCount)));
            }

            var returnedIds = new int[] { 3, 14, 22, 23, 51 };
            await idRegistry.CompleteReservations(SearchTypes.BoundSource, new string[] { reservationId }, unusedIds: returnedIds);

            var reservation1 = await idRegistry.ReserveIds(SearchTypes.BoundSource);
            var expectedIds = returnedIds.Concat(
                    Enumerable.Range(iterations * ElasticSearchIdRegistry.ReserveCount, ElasticSearchIdRegistry.ReserveCount - returnedIds.Length)).ToArray();
            Assert.True(new HashSet<int>(reservation1.ReservedIds).SetEquals(expectedIds));
        }

        [Test]
        public async Task ReservingStableIdsUsingRegistration()
        {
            var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ClearIndicesBeforeUse = true,
                ShardCount = 1,
                Prefix = "estest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            await store.InitializeAsync();

            var idRegistry = new ElasticSearchIdRegistry(store);

            List<TestStableIdItem> testStableIdItems = new List<TestStableIdItem>()
            {
                new TestStableIdItem(SearchTypes.BoundSource, 23, expectedStableId: 0),
                new TestStableIdItem(SearchTypes.BoundSource, 23, expectedStableId: 1) { Unused = true },
                new TestStableIdItem(SearchTypes.BoundSource, 23, expectedStableId: 2),
                new TestStableIdItem(SearchTypes.TextSource, 1, expectedStableId: 0),
                new TestStableIdItem(SearchTypes.TextSource, 1, expectedStableId: 1),
                new TestStableIdItem(SearchTypes.Project, 5, expectedStableId: 0),
                new TestStableIdItem(SearchTypes.Project, 4, expectedStableId: 1) { Unused = true },
                new TestStableIdItem(SearchTypes.Project, 4, expectedStableId: 1),
                new TestStableIdItem(SearchTypes.Project, 3, expectedStableId: 3),
                new TestStableIdItem(SearchTypes.BoundSource, 23, expectedStableId: 3),
                new TestStableIdItem(SearchTypes.BoundSource, 23, expectedStableId: 2),
            };

            await idRegistry.SetStableIdsAsync(testStableIdItems);

            var uids = new HashSet<(SearchType, string)>();

            foreach (var item in testStableIdItems)
            {
                Assert.IsTrue(item.IsAdded == uids.Add((item.SearchType, item.Uid)));
                Assert.IsTrue(item.StableIdValue.HasValue);
                Assert.AreEqual(item.ExpectedStableId, item.StableId);
            }

            await idRegistry.FinalizeAsync();

            // TODO: Test that stable id marker documents contains correct free list and empty pending reservations
        }

        private class TestStableIdItem : IStableIdItem
        {
            public int StableIdGroup { get; }
            public bool IsAdded { get; set; }

            // TODO: What is this used for?
            public bool Unused { get; set; }
            public int? StableIdValue { get; set; }
            public int ExpectedStableId { get; }
            public int StableId { get => StableIdValue.Value; set => StableIdValue = value; }
            public SearchType SearchType { get; }
            public string Uid { get; }

            public TestStableIdItem(SearchType searchType, int stableIdGroup, int expectedStableId)
            {
                SearchType = searchType;
                StableIdGroup = stableIdGroup;
                ExpectedStableId = expectedStableId;
                Uid = $"{stableIdGroup}:{expectedStableId}";
            }

            public override string ToString()
            {
                return $"Expect: {ExpectedStableId}, Actual: {StableIdValue}, Match: {ExpectedStableId == StableIdValue}";
            }
        }

        [Test]
        public async Task TestSearch()
        {
            var codex = new ElasticSearchCodex(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ShardCount = 1,
                Prefix = "estest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            var response = await codex.SearchAsync(new SearchArguments()
            {
                SearchString = "assem"
            });

            Assert.IsNull(response.Error);
        }

        // TODO: Fix these
        //[Test]
        //public async Task StoredFilterTest()
        //{
        //    const int valuesToAdd = 1012;

        //    var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
        //    {
        //        CreateIndices = true,
        //        ShardCount = 1,
        //        Prefix = "estest."
        //    }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

        //    await store.Clear();

        //    await store.InitializeAsync();

        //    Random random = new Random(12);

        //    Dictionary<int, HashSet<int>> valuesMap = new Dictionary<int, HashSet<int>>();

        //    HashSet<int> valuesToStore = new HashSet<int>();

        //    for (int i = 0; i < valuesToAdd; i++)
        //    {
        //        valuesToStore.Add(random.Next(valuesToAdd * 100, valuesToAdd * 200));
        //    }

        //    // Store initial filter
        //    var filter1 = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

        //    // Verify that adding same values DOES NOT change filter
        //    var filter1_same = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

        //    Assert.AreEqual(filter1.FilterHash, filter1_same.FilterHash);

        //    Assert.True(filter1.Filter.SequenceEqual(filter1_same.Filter), "Filter should be the same if unioned with same values");

        //    valuesToStore.Clear();
        //    for (int i = 0; i < valuesToAdd; i++)
        //    {
        //        valuesToStore.Add(random.Next(valuesToAdd * 200, valuesToAdd * 300));
        //    }

        //    // Store initial filter
        //    var filter2 = await StoreAndVerifyFilter(store, valuesMap, valuesToStore, filterId: 2);

        //    Assert.AreNotEqual(filter1.FilterHash, filter2.FilterHash);

        //    valuesToStore.Clear();
        //    for (int i = 0; i < valuesToAdd; i++)
        //    {
        //        valuesToStore.Add(random.Next(valuesToAdd * 200, valuesToAdd * 300));
        //    }

        //    // Verify that adding different values DOES change filter
        //    var filter1b = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

        //    Assert.AreNotEqual(filter1.FilterHash, filter1b.FilterHash);
        //    Assert.False(filter1.Filter.SequenceEqual(filter1b.Filter), "Filter should be the same if unioned with same values");
        //}

        //private async Task<IStoredFilter> StoreAndVerifyFilter(
        //    ElasticSearchStore store,
        //    Dictionary<int, HashSet<int>> valuesMap,
        //    IEnumerable<int> valuesToStore,
        //    int filterId = 1,
        //    [CallerLineNumber] int line = 0)
        //{
        //    var values = valuesMap.GetOrAdd(filterId, new HashSet<int>());
        //    valuesToStore = valuesToStore.ToList();

        //    values.UnionWith(valuesToStore);

        //    await store.RegisteredEntityStore.StoreAsync(
        //        valuesToStore.Select(stableId =>
        //        {
        //            return new RegisteredEntity()
        //            {
        //                Uid = GetUidFromStableId(stableId),
        //                DateAdded = DateTime.UtcNow,
        //                StableId = stableId,
        //            };
        //        }).ToArray());

        //    await store.StoredFilterStore.RefreshAsync();

        //    string baseFilterId = "TEST_STORED_FILTER#" + filterId;
        //    string storedFilterId = StoredFilterUtilities.GetFilterId(baseFilterId, store.RegisteredEntityStore.IndexName, 0);
        //    await store.StoredFilterStore.UpdateStoredFiltersAsync(new[]
        //    {
        //        new StoredFilter()
        //        {
        //            Uid = storedFilterId,
        //            StableIds = valuesToStore.ToList(),
        //            FilterHash = string.Empty,
        //        }
        //    });

        //    var retrievedFilterResponse = await store.StoredFilterStore.GetAsync(storedFilterId);
        //    var retrievedFilter = retrievedFilterResponse.Result;

        //    await store.StoredFilterStore.RefreshAsync();
        //    var retrievedRefreshFilter = (await store.StoredFilterStore.GetAsync(storedFilterId)).Result;

        //    Assert.AreEqual(values.Count, retrievedRefreshFilter.FilterCount, $"Caller Line: {line}");// Sequence: '{string.Join(", ", values.OrderBy(v => v))}'");
        //    Assert.AreEqual(values.Count, retrievedFilter.FilterCount, $"Caller Line: {line}");
        //    Assert.AreNotEqual(string.Empty, retrievedFilter.FilterHash, $"Caller Line: {line}");

        //    await store.RegisteredEntityStore.RefreshAsync();

        //    var filteredEntitiesResponse = await store.RegisteredEntityStore.GetStoredFilterEntities(baseFilterId,
        //        // Ensure that if there are more matches than expected that the API would return those results
        //        maxCount: values.Count + 1);
        //    var filteredEntities = filteredEntitiesResponse.Result;

        //    var filteredEntityIds = new HashSet<int>(filteredEntities.Select(e => e.StableId));

        //    var missingFilteredEntityIds = values.Except(filteredEntityIds).ToList();
        //    Assert.IsEmpty(missingFilteredEntityIds);

        //    var extraFilteredEntityIds = filteredEntityIds.Except(values).ToList();
        //    Assert.IsEmpty(extraFilteredEntityIds);

        //    Assert.AreEqual(values.Count, filteredEntities.Count, $"Caller Line: {line}");

        //    return retrievedFilter;
        //}

        private string GetUidFromStableId(int stableId)
        {
            return Convert.ToString(stableId, 16);
        }
    }
}
