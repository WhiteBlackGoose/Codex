using Codex.ElasticSearch.Formats;
using Codex.ElasticSearch.Search;
using Codex.ElasticSearch.Store;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Serialization;
using Codex.Utilities;
using CodexTestCSharpLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            (var store, var codex) = await InitializeAsync("estest.", populateCount: 0);
            await store.RegisteredEntityStore.RefreshAsync();

            await store.DefinitionStore.RefreshAsync();
            await store.StoredFilterStore.RefreshAsync();

            // Try searching for AssemblyCompanyAttribute which is defined outside of the 
            // ingested code using two modes:
            // AllowReferencedDefinitions = false (i.e. only symbols declared in ingested code should be returned in results)
            // AllowReferencedDefinitions = true (i.e. symbols referenced by ingested code may be returned in results)
            var arguments = new SearchArguments()
            {
                SearchString = nameof(AssemblyCompanyAttribute),
                AllowReferencedDefinitions = false,
                FallbackToTextSearch = false
            };

            var declaredSearch = await codex.SearchAsync(arguments);
            Assert.AreEqual(0, declaredSearch.Result.Total);

            arguments.AllowReferencedDefinitions = true;
            var allSearch = await codex.SearchAsync(arguments);
            Assert.True(allSearch.Result.Total > 0, "Search allowing referenced definitions should return some results");
            Assert.True(allSearch.Result.Hits.Where(h => h.Definition != null).Count() > 0, "Search allowing referenced definitions should have definition results");
        }

        [Test]
        public async Task TestPrefix()
        {
            bool populate = true;

            (var store, var codex) = await InitializeAsync("estest.", populateCount: 0, clear: populate, activeIndices: new[]
                {
                    SearchTypes.Definition,
                });

            Dictionary<string, DefinitionSymbol> defMap = new Dictionary<string, DefinitionSymbol>();

            var definitions = new[]
                {
                    new DefinitionSymbol()
                    {
                        ShortName = "XedocBaseObjectDefinition",
                    },
                    new DefinitionSymbol()
                    {
                        ShortName = "XedXedoc",
                    },
                    new DefinitionSymbol()
                    {
                        ShortName = "NotXedoc",
                    },
                    new DefinitionSymbol()
                    {
                        ContainerQualifiedName = "Codex.Test1",
                        ShortName = "XedocAbstract",
                    },
                    new DefinitionSymbol()
                    {
                        ContainerQualifiedName = "Codex.Test2",
                        ShortName = "XedocImpl",
                    },
                    new DefinitionSymbol()
                    {
                        ContainerQualifiedName = "Index.Test1",
                        ShortName = "XedocImplementer",
                    },
                    new DefinitionSymbol()
                    {
                        ShortName = "XedocInterface",
                    }
                };

            foreach (var def in definitions)
            {
                defMap[string.Join(".", def.ContainerQualifiedName, def.ShortName)] = def;
            }

            if (populate)
            {
                await store.DefinitionStore.AddDefinitions(definitions);

                await store.DefinitionStore.RefreshAsync();
            }

            var arguments = new SearchArguments()
            {
                RepositoryScopeId = SearchArguments.AllRepositoryScopeId,
                AllowReferencedDefinitions = false,
                FallbackToTextSearch = false,
                TextSearch = false
            };

            DefinitionSymbol[] noDefinitions = null;

            await verify("xedoci");
            await verify("xedoc");
            await verify("xed");
            await verify("none");
            await verify("impl");
            await verify("*imp");
            await verify("*abs");
            await verify("*xed");
            await verify("xed");
            await verify("xbo");
            await verify("xbod");
            await verify("xedn");
            await verify("xedp");

            await verify("*mpl", noDefinitions);
            await verify("test1.xedoc",
                defMap["Codex.Test1.XedocAbstract"],
                defMap["Index.Test1.XedocImplementer"]);

            await verify("index.test1.xedoc",
                defMap["Index.Test1.XedocImplementer"]);

            async Task verify(string searchText, params DefinitionSymbol[] expectedDefinitionsOverride)
            {
                Func<DefinitionSymbol, bool> predicate = d => d.ShortName.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) || d.AbbreviatedName?.StartsWith(searchText, StringComparison.OrdinalIgnoreCase) == true;

                if (expectedDefinitionsOverride == null)
                {
                    predicate = d => false;
                }
                else if (expectedDefinitionsOverride.Length != 0)
                {
                    var definitionSet = new HashSet<DefinitionSymbol>(expectedDefinitionsOverride, new EqualityComparerBuilder<DefinitionSymbol>().CompareByAfter(d => d.ShortName).CompareByAfter(d => d.ContainerQualifiedName));
                    predicate = d => definitionSet.Contains(d);
                }
                else if (searchText.StartsWith("*"))
                {
                    predicate = d => d.ShortName.IndexOf(searchText.Trim('*'), StringComparison.OrdinalIgnoreCase) >= 0;
                }

                arguments.SearchString = searchText;
                var response = await codex.SearchAsync(arguments);

                Console.WriteLine($"Found {response.Result?.Total ?? -1} results for '{searchText}': Error='{response.Error}'");

                CollectionAssert.AreEquivalent(
                    definitions.Where(predicate).Select(d => d.ShortName),
                    response.Result.Hits.Select(s => s.Definition.ShortName),
                    $"Should find all results for: {searchText}:" 
                    + Environment.NewLine
                    + string.Join(Environment.NewLine, response.RawQueries));
            }
        }

        [Test]
        public async Task TestSearch()
        {
            (var store, var codex) = await InitializeAsync("estest.", populateCount: 1);
            await store.RegisteredEntityStore.RefreshAsync();

            await store.DefinitionStore.RefreshAsync();

            await store.StoredFilterStore.RefreshAsync();

            var arguments = new SearchArguments()
            {
                SearchString = "xedocb",
                AllowReferencedDefinitions = false,
                TextSearch = false
            };

            var results = await codex.SearchAsync(arguments);
        }

        [Test]
        public async Task TestTextSearch()
        {
            (var store, var codex) = await InitializeAsync("text.estest.", populateCount: 1);
            await store.RegisteredEntityStore.RefreshAsync();

            await store.TextChunkStore.RefreshAsync();

            await store.TextSourceStore.RefreshAsync();

            var arguments = new SearchArguments()
            {
                SearchString = "Comment with same text",
                AllowReferencedDefinitions = false,
                TextSearch = true
            };

            var textSearchResult = await codex.SearchAsync(arguments);
            var hits = textSearchResult.Result.Hits;

            var expected = new (int lineNumber, string text)[]
            {
                (TextSearch.CommentWithSameTextLineNumber1, "Comment with same text"),
                (TextSearch.CommentWithSameTextLineNumber2, "Comment with SAME text"),
                (TextSearch.MultiCommentWithSameTextLineNumber3, "comment"),
                (TextSearch.MultiCommentWithSameTextLineNumber3 + 1, "with same text"),
            };

            Assert.AreEqual(expected.Select(s => s.lineNumber), hits.Select(s => s.TextLine.TextSpan.LineNumber));
            Assert.AreEqual(expected.Select(s => s.text), hits.Select(s => s.TextLine.TextSpan.GetSegment()));
        }

        private async Task<(ElasticSearchStore store, ElasticSearchCodex codex)> InitializeAsync(
            string prefix,
            int populateCount,
            bool clear = false,
            SearchType[] activeIndices = null)
        {
            bool populate = populateCount > 0;

            ElasticSearchStoreConfiguration configuration = new ElasticSearchStoreConfiguration()
            {
                ActiveIndices = activeIndices != null
                    ? new HashSet<SearchType>(activeIndices)
                    : null,
                ClearIndicesBeforeUse = populate || clear,
                CreateIndices = populate || clear,
                ShardCount = 1,
                Prefix = prefix
            };
            ElasticSearchService service = new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200"));
            var store = new ElasticSearchStore(configuration, service);

            await store.InitializeAsync();

            if (populate)
            {
                DirectoryCodexStore originalStore = DirectoryCodexStoreTests.CreateInputStore();
                await store.FinalizeAsync();

                for (int i = 0; i < populateCount; i++)
                {
                    await originalStore.ReadAsync(store);
                }
            }

            var codex = new ElasticSearchCodex(configuration, service);

            return (store, codex);
        }

        [Test]
        public async Task TestExhaustive()
        {
            (var store, var codex) = await InitializeAsync("test.", populateCount: 0);
            await store.RegisteredEntityStore.RefreshAsync();

            foreach (var searchType in SearchTypes.RegisteredSearchTypes.Where(s => s == SearchTypes.BoundSource))
            {
                var searchEntityInfo = await codex.GetSearchEntityInfoAsync(searchType);

                var searchEntityMap = searchEntityInfo.Result.Hits.ToLookup(s => s.Uid);
                var registeredEntities = await codex.GetRegisteredEntitiesAsync(searchType);

                var registeredEntityMap = registeredEntities.Result.Hits.ToLookup(s => s.EntityUid);

                var leftKeys = searchEntityMap.Select(s => s.Key);
                var rightKeys = registeredEntityMap.Select(s => s.Key);

                var leftOnlyKeys = leftKeys.Except(rightKeys).ToList();
                var rightOnlyKeys = rightKeys.Except(leftKeys).ToList();
            }
        }

        [Test]
        public async Task TestDelta()
        {
            (var store, var codex) = await InitializeAsync("test.", populateCount: 0);
            await store.RegisteredEntityStore.RefreshAsync();

            var leftName = "domino.190125.054145";
            var rightName = "domino.190125.055104";
            string root = @"D:\temp\diff";

            await EmitBoundFilesDiff(codex, leftName, rightName, root);
            await EmitBoundFilesDiff(codex, rightName, leftName, root);
        }

        private static async Task EmitBoundFilesDiff(ElasticSearchCodex codex, string leftName, string rightName, string root)
        {
            if (Directory.Exists(Path.Combine(root, leftName)))
            {
                Directory.Delete(Path.Combine(root, leftName), recursive: true);
            }

            await EmitTextFilesDiff(codex, leftName, rightName, root);
            //return;

            var searchType = SearchTypes.BoundSource;
            var leftEntities = await codex.GetLeftOnlyEntitiesAsync(searchType, leftName, rightName);

            var leftRoot = Path.Combine(root, leftName, searchType.Name);
            Directory.CreateDirectory(leftRoot);
            foreach (var entity in leftEntities.Result.Hits)
            {
                File.WriteAllText(Path.Combine(leftRoot, $"{entity.File.Info.ProjectId}_{Path.GetFileName(entity.File.Info.ProjectRelativePath)}.json"), entity.ElasticSerialize());
            }
        }

        private static async Task EmitTextFilesDiff(ElasticSearchCodex codex, string leftName, string rightName, string root)
        {
            if (Directory.Exists(Path.Combine(root, leftName)))
            {
                Directory.Delete(Path.Combine(root, leftName), recursive: true);
            }
            var searchType = SearchTypes.TextSource;
            var leftEntities = await codex.GetLeftOnlyEntitiesAsync(searchType, leftName, rightName);

            var leftRoot = Path.Combine(root, leftName, searchType.Name);
            Directory.CreateDirectory(leftRoot);
            foreach (var entity in leftEntities.Result.Hits)
            {
                File.WriteAllText(Path.Combine(leftRoot, $"{entity.File.Info.ProjectId}_{Path.GetFileName(entity.File.Info.ProjectRelativePath)}.json"), entity.ElasticSerialize());
            }
        }

        [Test]
        public async Task TestStoreEntityDoesNotReplace()
        {
            (var store, var codex) = await InitializeAsync("estest.", populateCount: 1);
            await store.RegisteredEntityStore.RefreshAsync();

            var priorValue = "Before";

            var entity = new RegisteredEntity()
            {
                Uid = "1iLAiQFJ7U+k0bYx7uqfyg",
                EntityVersion = 2,
                EntityContentId = priorValue
            };

            await store.RegisteredEntityStore.AddAsync(new[] { entity });
            await store.RegisteredEntityStore.RefreshAsync();

            var retrievedEntity = await store.RegisteredEntityStore.GetAsync(entity.Uid);

            entity.EntityContentId = "After";
            await store.RegisteredEntityStore.AddAsync(new[] { entity });
            await store.RegisteredEntityStore.RefreshAsync();

            retrievedEntity = await store.RegisteredEntityStore.GetAsync(entity.Uid);
            Assert.AreEqual(priorValue, retrievedEntity.Result.EntityContentId);

            // Now try with higher version number
            entity.EntityVersion += 10;
            await store.RegisteredEntityStore.AddAsync(new[] { entity });
            await store.RegisteredEntityStore.RefreshAsync();

            retrievedEntity = await store.RegisteredEntityStore.GetAsync(entity.Uid);
            Assert.AreEqual(priorValue, retrievedEntity.Result.EntityContentId);
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
            public bool IsCommitted { get; set; }

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

            var filteredEntityIds = new HashSet<int>(filteredEntities.Select(e => (int)e.StableId));

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
