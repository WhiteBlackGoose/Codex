using Codex.ObjectModel;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            for (int i = 0; i < 10; i++)
            {
                values.Add(random.Next(0, 100000));
            }

            await store.RegisteredEntityStore.StoreAsync(
                values.Select(stableId =>
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
            await store.StoredFilterStore.StoreAsync(new[]
            {
                new StoredFilter()
                {
                    Uid = storedFilterId,
                    StableIds = values.ToList()
                }
            });
        }

        private string GetUidFromStableId(long stableId)
        {
            return Convert.ToString(stableId, 16);
        }
    }
}
