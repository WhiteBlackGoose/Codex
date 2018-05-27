using Nest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using Elasticsearch.Net;
using Newtonsoft.Json;
using Codex.Serialization;
using Codex.Storage.ElasticProviders;
using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.ElasticSearch.Search;
using Nest.JsonNetSerializer;

namespace Codex.ElasticSearch
{
    public class ElasticSearchService
    {
        private ElasticClient client;
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly ElasticSearchServiceConfiguration configuration;
        private readonly ConnectionSettings settings;

        public ElasticSearchService(ElasticSearchServiceConfiguration configuration)
        {
            this.configuration = configuration;
            this.settings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri(configuration.Endpoint)))
                .EnableHttpCompression();

            if (configuration.CaptureRequests)
            {
                settings = settings.PrettyJson().DisableDirectStreaming().DefaultTypeNameInferrer(type =>
                {
                    return ElasticCodexTypeUtilities.Instance.GetImplementationType(type).Name.ToLowerInvariant();
                });
            }

            client = new ElasticClient(settings);
        }
        
        public async Task<ElasticSearchResponse<T>> UseClient<T>(Func<ClientContext, Task<T>> useClient)
        {
            var startTime = stopwatch.Elapsed;
            T result;
            var context = new ClientContext()
            {
                CaptureRequests = configuration.CaptureRequests,
                Client = this.client
            };

            result = await useClient(context);

            return new ElasticSearchResponse<T>()
            {
                Requests = context.Requests,
                Duration = stopwatch.Elapsed - startTime,
                Result = result
            };
        }

        public Task ClearAsync()
        {
            return DeleteIndexAsync(Indices.All);
        }

        public async Task<bool> DeleteIndexAsync(Indices indices)
        {
            var result = await UseClient(async context =>
            {
                var existsQuery = (await client.IndexExistsAsync(indices)).ThrowOnFailure();
                if (!existsQuery.Exists)
                {
                    return false;
                }

                var response = await context.Client.DeleteIndexAsync(indices);
                response.ThrowOnFailure();
                return true;
            });

            return result.Result;
        }

        public async Task<IEnumerable<(string IndexName, bool IsActive)>> GetIndicesAsync()
        {
            var response = await UseClient(async context =>
            {
                var client = context.Client;

                var result = await client.GetAliasAsync().ThrowOnFailure();

                return result.Indices.Select(kvp =>
                (
                    IndexName: kvp.Key.ToString(),
                    IsActive: Placeholder.Value<bool>("Is this still applicable?")
                ))
                .OrderBy(v => v.IndexName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            });

            return response.Result;
        }

        public async Task<ElasticSearchStore> CreateStoreAsync(ElasticSearchStoreConfiguration configuration)
        {
            var store = new ElasticSearchStore(configuration, this);
            await store.InitializeAsync();
            return store;
        }

        public async Task<ElasticSearchCodex> CreateCodexAsync(ElasticSearchStoreConfiguration configuration)
        {
            var store = new ElasticSearchCodex(configuration, this);
            return store;
        }
    }

    public class OverrideConnectionSettings : ConnectionSettings
    {
        public OverrideConnectionSettings(Uri uri) 
            : base(new SingleNodeConnectionPool(uri), sourceSerializer: CreateSerializer)
        {
        }

        private static IElasticsearchSerializer CreateSerializer(IElasticsearchSerializer builtIn, IConnectionSettingsValues values)
        {
            return new EntityJsonNetSerializer(builtIn, values);
        }

        public IElasticsearchSerializer GetSerializer()
        {
            return new EntityJsonNetSerializer(null, new ConnectionSettings());
        }
    }

    public class ClientContext
    {
        // TODO: Disable
        public bool CaptureRequests = true;
        public ElasticClient Client;
        public List<string> Requests = new List<string>();
    }

    public class ElasticSearchResponse<T>
    {
        public IReadOnlyList<string> Requests { get; set; } = CollectionUtilities.Empty<string>.Array;
        public Exception Exception { get; set; }
        public T Result { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ElasticSearchServiceConfiguration
    {
        public string Endpoint { get; set; }
        public bool CaptureRequests { get; set; } = true;

        public ElasticSearchServiceConfiguration(string endpoint)
        {
            Endpoint = endpoint;
        }
    }
}
