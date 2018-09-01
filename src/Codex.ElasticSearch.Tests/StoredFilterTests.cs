using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Nest;
using static Nest.Infer;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Codex.ElasticSearch.Store;
using Codex.ElasticSearch.Formats;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class StoredFilterTests
    {
        [Test]
        public async Task TestFilterTree()
        {
            var testStore = new TestFilterStore();
            StoredFilterManager manager = new StoredFilterManager(testStore);

            const string filterKey = "fruits";

            await manager.AddStoredFilterAsync(filterKey, "apple", CreateStoredFilter(1, 4, 7));
            await manager.AddStoredFilterAsync(filterKey, "banana", CreateStoredFilter(2, 5, 7));
            await manager.AddStoredFilterAsync(filterKey, "cherry", CreateStoredFilter(3, 9));

            var filter = testStore.FilterMap[filterKey];

            Assert.AreEqual(expected: new[] { 1, 2, 3, 4, 5, 7, 9 }, actual: filter.GetStableIdValues().ToArray());

            // Now replace apple such that final filter should not have 1 or 4
            await manager.AddStoredFilterAsync(filterKey, "apple", CreateStoredFilter(2, 3));

            Assert.AreEqual(expected: new[] { 2, 3, 5, 7, 9 }, actual: filter.GetStableIdValues().ToArray());

            // Now remove banana so that filter should be apple=(2, 3) + cherry=(3, 9)
            await manager.RemoveStoredFilterAsync(filterKey, "banana");

            Assert.AreEqual(expected: new[] { 2, 3, 9 }, actual: filter.GetStableIdValues().ToArray());
        }

        public StoredFilter CreateStoredFilter(params int[] stableIds)
        {
            var filter = new StoredFilter().ApplyStableIds(stableIds.OrderBy(id => id));
            filter.PopulateContentIdAndSize();
            return filter;
        }

        public class TestFilterStore : IEntityStore<IStoredFilter>
        {
            public readonly Dictionary<string, StoredFilter> FilterMap = new Dictionary<string, StoredFilter>();

            public async Task<IReadOnlyList<IStoredFilter>> GetAsync(IReadOnlyList<string> uids)
            {
                await Task.Yield();
                lock (this)
                {
                    return uids.Select(uid => FilterMap.GetOrDefault(uid)).ToList();
                }
            }

            public async Task StoreAsync(IReadOnlyList<IStoredFilter> entities)
            {
                await Task.Yield();
                lock (this)
                {
                    foreach (var entity in entities)
                    {
                        FilterMap[entity.Uid] = (StoredFilter)entity;
                    }
                }
            }
        }
    }
}
