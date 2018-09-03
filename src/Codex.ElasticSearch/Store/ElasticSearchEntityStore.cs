using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Elasticsearch.Net;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Codex.ElasticSearch.StoredFilterUtilities;

namespace Codex.ElasticSearch
{
    public abstract class ElasticSearchEntityStore
    {
        internal readonly SearchType SearchType;
        internal readonly ElasticSearchStore Store;
        public readonly string IndexName;
        public int ShardCount { get; protected set; }

        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
        {
            this.Store = store;
            this.SearchType = searchType;
            this.IndexName = GetIndexName(store, searchType);
        }

        public static string GetIndexName(ElasticSearchStore store, SearchType searchType)
        {
            return (store.Configuration.Prefix + searchType.IndexName).ToLowerInvariant();
        }

        public abstract Task InitializeAsync();
    }

    public class ElasticSearchEntityStore<T> : ElasticSearchEntityStore, IStore<T>, IEntityStore<T>
        where T : class, ISearchEntity
    {
        internal readonly SearchType<T> EntitySearchType;

        public ElasticSearchEntityStore(ElasticSearchStore store, SearchType searchType)
            : base(store, searchType)
        {
            EntitySearchType = (SearchType<T>)searchType;
        }

        public override async Task InitializeAsync()
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

        public Task FinalizeAsync()
        {
            return Task.CompletedTask;
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
                        c => c.Mappings(m => m.Map<T>(tm => tm.AutoMapEx()))
                            .Settings(s => s
                                .AddAnalyzerSettings()
                                .Setting("index.mapper.dynamic", false)
                                .NumberOfShards(SearchType == SearchTypes.StoredFilter ? 1 : Store.Configuration.ShardCount)
                                .RefreshInterval(TimeSpan.FromMinutes(1)))
                            .CaptureRequest(context))
                            .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public async Task RefreshAsync()
        {
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client.RefreshAsync(IndexName).ThrowOnFailure();
                return response.IsValid;
            });
        }

        public Task<ElasticSearchResponse<T>> GetAsync(string uid)
        {
            return Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .GetAsync<T>(uid, g => g.Routing(GetRouting(uid)).Index(IndexName))
                    .ThrowOnFailure();

                return response.Source;
            });
        }

        public async Task DeleteAsync(IEnumerable<string> uids)
        {
            await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .BulkAsync(b => b.DeleteMany<T>(uids, (bd, uid) => bd
                        .Id(uid)
                        .Routing(GetRouting(uid))
                        .Index(IndexName)).CaptureRequest(context))
                    .ThrowOnFailure();

                return response.IsValid;
            });
        }

        public BulkDescriptor AddIndexOperation(BulkDescriptor bd, T value, bool replace = false)
        {
            if (replace)
            {
                return bd.Index<T>(bco => bco
                    .Document(value)
                    .Id(value.Uid)
                    .Routing(GetRouting(value.Uid))
                    .Index(IndexName)
                    .Version(value.EntityVersion)
                    .VersionType(value.EntityVersion.HasValue ? VersionType.External : VersionType.Internal));
            }
            else
            {
                return bd.Create<T>(bco => bco
                    .Document(value)
                    .Id(value.Uid)
                    .Routing(GetRouting(value.Uid))
                    .Index(IndexName)
                    .VersionType(value.EntityVersion.HasValue ? VersionType.External : VersionType.Internal)
                    .Version(value.EntityVersion));
            }
        }

        public async Task StoreAsync<TOut>(IReadOnlyList<T> values, UpdateMergeFunction<T> updateMergeFunction, bool replace = false)
            where TOut : class, T
        {
            await Store.Service.UseClient(async context =>
            {
                bool update = updateMergeFunction != null;
                replace |= update;
                var client = context.Client;


                if (update)
                {
                    T[] updatedValues = new T[values.Count];

                    var getResponse = client
                        .MultiGet(mg => mg.GetMany<TOut>(values.Select(value => value.Uid), 
                            (g, uid) => g.Routing(GetRouting(uid).ToString(CultureInfo.InvariantCulture)))
                            .Index(IndexName))
                            .ThrowOnFailure();

                    int index = 0;
                    foreach (var item in getResponse.Hits)
                    {
                        int currentIndex = index;
                        index++;
                        T value;
                        if (item.Found)
                        {
                            var oldValue = (T)item.Source;
                            value = updateMergeFunction(oldValue, values[currentIndex]);
                            value.EntityVersion = item.Version;
                        }
                        else
                        {
                            value = values[currentIndex];
                        }

                        updatedValues[currentIndex] = value;

                    }

                    values = updatedValues;
                }


                var response = await client
                    .BulkAsync(b => b.ForEach(values, (bd, value) => AddIndexOperation(bd, value, replace)).CaptureRequest(context));

                if (replace || !response.ApiCall.Success)
                {
                    response.ThrowOnFailure();
                }

                return response.IsValid;
            });
        }

        public Task StoreAsync(IReadOnlyList<T> values)
        {
            return StoreAsync<T>(values, updateMergeFunction: null, replace: true);
        }

        public async Task<IReadOnlyList<T>> GetAsync(IReadOnlyList<string> uids)
        {
            var result = await Store.Service.UseClient(async context =>
            {
                var response = await context.Client
                    .MultiGetAsync(mg => mg.GetMany<T>(uids, (g, uid) => g.Routing(GetRouting(uid))).Index(IndexName))
                    .ThrowOnFailure();

                return response.Hits.Select(g => (T)g.Source).ToList();
            });

            return result.Result;
        }
    }
}
