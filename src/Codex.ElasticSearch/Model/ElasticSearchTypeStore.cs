using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    internal class TypedStore<TSearchType>
    {
        private readonly SearchType searchType;
        private readonly ElasticSearchStore store;
        private readonly string indexName;

        public TypedStore(ElasticSearchStore store, SearchType searchType)
        {
            this.store = store;
            this.searchType = searchType;
            this.indexName = store.Configuration.Prefix + indexName;
        }

        public async Task InitializeAsync()
        {
            if (store.Configuration.CreateIndices)
            {
                await CreateIndexAsync();
            }
        }

        private async Task CreateIndexAsync()
        {
            var request = new Box<string>();

            await store.Service.UseClient(async client =>
            {
                var response = await client
                    .CreateIndexAsync(searchType.IndexName,
                        c => CustomAnalyzers.AddNGramAnalyzerFunc(c)
                            .Mappings(m => m.Map<T>(TypeIndexName<T>(), tm => tm.AutoMapEx()))
                            .CaptureRequest(client, request))
                    .ThrowOnFailure();
            });
        }
    }
}
