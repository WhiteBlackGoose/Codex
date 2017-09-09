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

namespace Codex.ElasticSearch
{
    internal class TypedStore<T> : IStore<T>
        where T : class
    {
        private readonly SearchType searchType;
        private readonly ElasticSearchStore store;
        private readonly string indexName;

        public TypedStore(ElasticSearchStore store, SearchType searchType)
        {
            this.store = store;
            this.searchType = searchType;
            this.indexName = store.Configuration.Prefix + searchType.IndexName;
        }

        public async Task InitializeAsync()
        {
            if (store.Configuration.CreateIndices)
            {
                await CreateIndexAsync();
            }

            // Change refresh interval
            // Disable replicas during indexing
        }

        public async Task FinalizeAsync()
        {

        }

        private async Task CreateIndexAsync()
        {
            var request = new Box<string>();

            await store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .CreateIndexAsync(indexName,
                        c => c.Mappings(m => m.Map<T>(TypeName.From<T>(), tm => tm.AutoMap(MappingPropertyVisitor.Instance)))
                            .Settings(s => s.AddAnalyzerSettings().NumberOfShards(store.Configuration.ShardCount).RefreshInterval(TimeSpan.FromMinutes(1)))
                            .CaptureRequest(context))
                    .ThrowOnFailure();
                
                return response.IsValid;
            });
        }

        public async Task StoreAsync(IReadOnlyList<T> values)
        {
            // TODO: Batch and create commits/stored filters
            // TODO: Handle updates
            await store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.ForEach(values, (bd, value) => bd.Create<T>(bco => bco.Document(value).Index(indexName))).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }
    }
}
