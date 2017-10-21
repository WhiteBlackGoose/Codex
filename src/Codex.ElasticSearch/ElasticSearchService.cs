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

namespace Codex.ElasticSearch
{
    public class ElasticSearchService
    {
        private ElasticClient client;
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly ElasticSearchServiceConfiguration configuration;

        public ElasticSearchService(ElasticSearchServiceConfiguration configuration)
        {
            this.configuration = configuration;
            var settings = new OverrideConnectionSettings(new Uri(configuration.Endpoint))
                .EnableHttpCompression();

            if (configuration.CaptureRequests)
            {
                settings = settings.PrettyJson().DisableDirectStreaming();
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

            try
            {
                result = await useClient(context);
            }
            catch (Exception ex)
            {
                return new ElasticSearchResponse<T>()
                {
                    Requests = context.Requests,
                    Duration = stopwatch.Elapsed - startTime,
                    Exception = ex
                };
            }

            return new ElasticSearchResponse<T>()
            {
                Requests = context.Requests,
                Duration = stopwatch.Elapsed - startTime,
                Result = result
            };
        }

        public Task ClearAsync()
        {
            return UseClient(async context =>
            {
                var response = await context.Client.DeleteIndexAsync(Indices.All);
                response.ThrowOnFailure();
                return true;
            });
        }

        public async Task<ElasticSearchStore> CreateStoreAsync(ElasticSearchStoreConfiguration configuration)
        {
            var store = new ElasticSearchStore(configuration, this);
            await store.InitializeAsync();
            return store;
        }

        private class OverrideConnectionSettings : ConnectionSettings
        {
            private Serializer SharedSerializer;
            public OverrideConnectionSettings(Uri uri = null) : base(uri)
            {
            }
            protected override IElasticsearchSerializer DefaultSerializer(ConnectionSettings settings)
            {
                if (SharedSerializer == null)
                {
                    SharedSerializer = new Serializer(settings);
                }

                return SharedSerializer;
            }

            private class Serializer : JsonNetSerializer
            {

                public Serializer(IConnectionSettingsValues settings)
                    : base(settings, ModifyJsonSerializerSettings)
                {
                }

                private static void ModifyJsonSerializerSettings(JsonSerializerSettings arg1, IConnectionSettingsValues arg2)
                {
                    arg1.ContractResolver = new CachingContractResolver(new CompositeEntityResolver(
                        new EntityContractResolver(ObjectStage.Index),
                        arg1.ContractResolver));
                }
            }
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
        public bool CaptureRequests { get; set; }

        public ElasticSearchServiceConfiguration(string endpoint)
        {
            Endpoint = endpoint;
        }
    }
}
