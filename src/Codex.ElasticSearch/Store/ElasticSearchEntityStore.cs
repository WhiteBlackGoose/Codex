using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Elasticsearch.Net;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public abstract class ElasticSearchEntityStore
    {
        internal readonly SearchType SearchType;
        internal readonly ElasticSearchStore Store;
        public readonly string IndexName;
        public readonly string RegistryIndexName;
        public int ShardCount { get; protected set; }

        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
        {
            this.Store = store;
            this.SearchType = searchType;
            this.IndexName = (store.Configuration.Prefix + searchType.IndexName).ToLowerInvariant();
            this.RegistryIndexName = IndexName + ".reg";
        }

        public abstract Task DeleteAsync(IEnumerable<string> uids);
    }

    public class ElasticSearchEntityStore<T> : ElasticSearchEntityStore, IStore<T>
        where T : class, ISearchEntity
    {
        private readonly string m_pipeline;

        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
            : base(store, searchType)
        {
            m_pipeline = searchType == SearchTypes.StoredFilter ?
                store.StoredFilterPipelineId : null;
        }

        public async Task InitializeAsync()
        {
            if (Store.Configuration.CreateIndices)
            {
                await CreateIndexAsync();
            }

            var settings = await Store.Service.UseClient(async context =>
            {
                var client = context.Client;
                return await client.GetIndexSettingsAsync(r => r.Index(IndexName));
            });

            ShardCount = settings.Result.Indices[IndexName].Settings.NumberOfShards.Value;

            Placeholder.Todo("Add some indication to configuration indicating whether this store was opened for write");
            Placeholder.Todo("Change refresh interval");
            Placeholder.Todo("Disable replicas");
        }

        public async Task FinalizeAsync()
        {

        }

        private async Task CreateIndexAsync()
        {
            var request = new Box<string>();

            await Store.Service.UseClient(async context =>
            {
                var existsResponse = await context.Client.IndexExistsAsync(IndexName)
                    .ThrowOnFailure();

                if (existsResponse.Exists)
                {
                    Placeholder.Todo("Update mappings?");
                    return false;
                }

                var response = await context.Client
                    .CreateIndexAsync(IndexName,
                        c => c.Mappings(m => m.Map<T>(SearchType.IndexName, tm => tm.AutoMapEx()))
                            .Settings(s => s
                                .AddAnalyzerSettings()
                                .Setting("index.mapper.dynamic", false)
                                .NumberOfShards(Store.Configuration.ShardCount)
                                .RefreshInterval(TimeSpan.FromMinutes(1)))
                            .CaptureRequest(context))
                            .ThrowOnFailure();

                response = await context.Client
                .CreateIndexAsync(RegistryIndexName,
                    c => c.Mappings(m => m.Map<IRegisteredEntity>(SearchTypes.RegisteredEntity.IndexName, tm => tm.AutoMapEx()))
                        .Settings(s => s
                            .AddAnalyzerSettings()
                            .Setting("index.mapper.dynamic", false)
                            .NumberOfShards(Store.Configuration.ShardCount)
                            .RefreshInterval(TimeSpan.FromMinutes(1)))
                        .CaptureRequest(context))
                        .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public override async Task DeleteAsync(IEnumerable<string> uids)
        {
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.DeleteMany<T>(uids, (bd, uid) => bd.Id(uid).Index(IndexName)).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public BulkDescriptor AddIndexOperation(BulkDescriptor bd, T value, bool replace = false)
        {
            if (replace)
            {
                return bd.Update<T>(bco => bco.Doc(value).Id(value.Uid).DocAsUpsert().Index(IndexName).Pipeline(m_pipeline));
            }
            else
            {
                return bd.Create<T>(bco => bco.Document(value).Id(value.Uid).Index(IndexName).Pipeline(m_pipeline));
            }
        }

        public BulkDescriptor AddRegisterOperation(BulkDescriptor bd, IRegisteredEntity value)
        {
            return bd.Create<IRegisteredEntity>(bco => bco.Document(value).Index(RegistryIndexName).Id(value.Uid));
        }

        public async Task StoreAsync(IReadOnlyList<T> values, bool replace = false)
        {
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.ForEach(values, (bd, value) => AddIndexOperation(bd, value, replace)).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public Task StoreAsync(IReadOnlyList<T> values)
        {
            throw new NotImplementedException();
        }
    }
}
