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
    public class ElasticSearchEntityStore
    {
        internal readonly SearchType SearchType;
        internal readonly ElasticSearchStore Store;
        public readonly string IndexName;
        public int ShardCount => Placeholder.Value<int>("Get shard count during initialize. May be different than configuration");

        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
        {
            this.Store = store;
            this.SearchType = searchType;
            this.IndexName = store.Configuration.Prefix + searchType.IndexName;
        }

        public async Task DeleteAsync(IEnumerable<string> uids)
        {
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.DeleteMany(uids, (bd, uid) => bd.Id(uid).Index(IndexName)).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }
    }

    public class ElasticSearchEntityStore<T> : ElasticSearchEntityStore, IStore<T>
        where T : class
    {
        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
            : base(store, searchType)
        {
        }

        public async Task InitializeAsync()
        {
            if (Store.Configuration.CreateIndices)
            {
                await CreateIndexAsync();
            }

            await Store.Service.UseClient(async context =>
            {
                var client = context.Client;

                await Placeholder.NotImplementedAsync("Get shard count");

                return None.Value;
            });

            // Change refresh interval
            // Disable replicas during indexing
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
                    Placeholder.NotImplemented("Update mappings?");
                    return false;
                }

                var response = await context.Client
                    .CreateIndexAsync(IndexName,
                        c => c.Mappings(m => m.Map<T>(TypeName.From<T>(), tm => tm.AutoMap(MappingPropertyVisitor.Instance)))
                            .Settings(s => s.AddAnalyzerSettings().NumberOfShards(Store.Configuration.ShardCount).RefreshInterval(TimeSpan.FromMinutes(1)))
                            .CaptureRequest(context))
                            .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public BulkDescriptor AddCreateOperation(BulkDescriptor bd, T value)
        {
            return bd.Create<T>(bco => bco.Document(value).Index(IndexName));
        }

        public void SetIds(IEnumerable<T> entities)
        {
            Placeholder.NotImplemented("Set content id and uid");
        }

        public void SetId(T entity)
        {
            Placeholder.NotImplemented("Set content id and uid");
        }

        public async Task StoreAsync(IReadOnlyList<T> values)
        {
            // TODO: Batch and create commits/stored filters
            // TODO: Handle updates
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.ForEach(values, (bd, value) => AddCreateOperation(bd, value)).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }
    }
}
