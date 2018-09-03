﻿using Codex.ElasticSearch.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

            ElasticSearchStoreConfiguration configuration = new ElasticSearchStoreConfiguration()
            {
                ClearIndicesBeforeUse = true,
                CreateIndices = true,
                ShardCount = 1,
                Prefix = "estest."
            };
            ElasticSearchService service = new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200"));
            var store = new ElasticSearchStore(configuration, service);

            await store.InitializeAsync();

            await originalStore.ReadAsync(store);

            await originalStore.ReadAsync(store);

            await store.DefinitionStore.RefreshAsync();

            var codex = new ElasticSearchCodex(configuration, service);

            // Try searching for AssemblyCompanyAttribute which is defined outside of the 
            // ingested code using two modes:
            // AllowReferencedDefinitions = false (i.e. only symbols declared in ingested code should be returned in results)
            // AllowReferencedDefinitions = true (i.e. symbols referenced by ingested code may be returned in results)
            var arguments = new SearchArguments()
            {
                SearchString = nameof(AssemblyCompanyAttribute),
                AllowReferencedDefinitions = false
            };

            var declaredSearch = await codex.SearchAsync(arguments);
            Assert.AreEqual(0, declaredSearch.Result.Total);

            arguments.AllowReferencedDefinitions = true;
            var allSearch = await codex.SearchAsync(arguments);
            Assert.True(allSearch.Result.Total > 0, "Search allowing referenced definitions should return some results");
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
        [Test]
        public async Task StoredFilterTest()
        {
            const int valuesToAdd = 1012;

            var store = new ElasticSearchStore(new ElasticSearchStoreConfiguration()
            {
                CreateIndices = true,
                ShardCount = 1,
                Prefix = "sftest."
            }, new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200")));

            await store.Clear();

            await store.InitializeAsync();

            Random random = new Random(12);

            Dictionary<int, HashSet<int>> valuesMap = new Dictionary<int, HashSet<int>>();

            HashSet<int> valuesToStore = new HashSet<int>();

            for (int i = 0; i < valuesToAdd; i++)
            {
                valuesToStore.Add(random.Next(valuesToAdd * 100, valuesToAdd * 200));
            }

            // Store initial filter
            var filter1 = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

            // Verify that adding same values DOES NOT change filter
            var filter1_same = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

            Assert.AreEqual(filter1.FilterHash, filter1_same.FilterHash);

            AssertFilterEquals(filter1, filter1_same, "Filter should be the same if unioned with same values");

            valuesToStore.Clear();
            for (int i = 0; i < valuesToAdd; i++)
            {
                valuesToStore.Add(random.Next(valuesToAdd * 200, valuesToAdd * 300));
            }

            // Store initial filter
            var filter2 = await StoreAndVerifyFilter(store, valuesMap, valuesToStore, filterId: 2);

            Assert.AreNotEqual(filter1.FilterHash, filter2.FilterHash);

            // Verify that filter 1 is unchanged
            var filter1Unchanged = await RetrieveAndVerifyFilter(store, new HashSet<int>(filter1.GetStableIdValues()), filter1.Uid);

            Assert.AreEqual(filter1.FilterHash, filter1Unchanged.FilterHash);
            AssertFilterEquals(filter1, filter1Unchanged, "Filter should be the same if not modifications were made");

            valuesToStore.Clear();
            for (int i = 0; i < valuesToAdd; i++)
            {
                valuesToStore.Add(random.Next(valuesToAdd * 200, valuesToAdd * 300));
            }

            await StoreValues(store, valuesToStore);

            // Verify that adding different values DOES change filter
            var filter1b = await StoreAndVerifyFilter(store, valuesMap, valuesToStore);

            Assert.AreNotEqual(filter1.FilterHash, filter1b.FilterHash);
            AssertFilterEquals(filter1, filter1b, "Filter should be the ovewritten.", equals: false);
        }

        private void AssertFilterEquals(IStoredFilter filter1, IStoredFilter filter2, string message, bool equals = true)
        {
            if (equals)
            {
                Assert.AreEqual(filter1.GetStableIdValues().ToList(), filter2.GetStableIdValues().ToList(), message);
            }
            else
            {
                Assert.AreNotEqual(filter1.GetStableIdValues().ToList(), filter2.GetStableIdValues().ToList(), message);
            }
        }

        private async Task<IStoredFilter> StoreAndVerifyFilter(
            ElasticSearchStore store,
            Dictionary<int, HashSet<int>> valuesMap,
            IEnumerable<int> valuesToStore,
            int filterId = 1,
            [CallerLineNumber] int line = 0)
        {
            var values = valuesMap.GetOrAdd(filterId, new HashSet<int>());
            valuesToStore = valuesToStore.ToList();

            values.UnionWith(valuesToStore);

            await StoreValues(store, valuesToStore);

            await store.StoredFilterStore.RefreshAsync();

            string storedFilterId = "TEST_STORED_FILTER#" + filterId;

            var storedFilter = new StoredFilter()
            {
                Uid = storedFilterId,
            }.ApplyStableIds(values.OrderBy(i => i));

            var desids = storedFilter.GetStableIdValues().ToList();

            await store.StoredFilterStore.UpdateStoredFiltersAsync(new[]
            {
                storedFilter
            });

            return await RetrieveAndVerifyFilter(store, values, storedFilterId, line);
        }

        private static async Task<IStoredFilter> RetrieveAndVerifyFilter(ElasticSearchStore store, HashSet<int> values, string storedFilterId, [CallerLineNumber] int line = 0)
        {
            await store.StoredFilterStore.RefreshAsync();
            var retrievedFilterResponse = await store.StoredFilterStore.GetAsync(storedFilterId);
            var retrievedFilter = retrievedFilterResponse.Result;

            Assert.AreEqual(values.Count, retrievedFilter.Cardinality, $"Caller Line: {line}");
            Assert.AreNotEqual(string.Empty, retrievedFilter.FilterHash, $"Caller Line: {line}");

            await store.RegisteredEntityStore.RefreshAsync();

            var filteredEntitiesResponse = await store.RegisteredEntityStore.GetStoredFilterEntities(storedFilterId,
                // Ensure that if there are more matches than expected that the API would return those results
                maxCount: values.Count + 1);
            var filteredEntities = filteredEntitiesResponse.Result;

            var filteredEntityIds = new HashSet<int>(filteredEntities.Select(e => e.StableId));

            var missingFilteredEntityIds = values.Except(filteredEntityIds).ToList();
            Assert.IsEmpty(missingFilteredEntityIds);

            var extraFilteredEntityIds = filteredEntityIds.Except(values).ToList();
            Assert.IsEmpty(extraFilteredEntityIds);

            Assert.AreEqual(values.Count, filteredEntities.Count, $"Caller Line: {line}");

            return retrievedFilter;
        }

        private async Task StoreValues(ElasticSearchStore store, IEnumerable<int> valuesToStore)
        {
            await store.RegisteredEntityStore.StoreAsync(
                valuesToStore.Select(stableId =>
                {
                    return new RegisteredEntity()
                    {
                        Uid = GetUidFromStableId(stableId),
                        DateAdded = DateTime.UtcNow,
                        StableId = stableId,
                    };
                }).ToArray());
        }

        private string GetUidFromStableId(int stableId)
        {
            return Convert.ToString(stableId, 16);
        }
    }
}
