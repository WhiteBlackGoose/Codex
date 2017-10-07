using Nest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.ElasticSearch
{
    public class ElasticSearchService
    {
        private ElasticClient client;
        private Stopwatch stopwatch = Stopwatch.StartNew();

        public ElasticSearchService(ElasticSearchServiceConfiguration configuration)
        {
            client = new ElasticClient(new Uri(configuration.Endpoint));
        }

        public async Task<ElasticSearchResponse<T>> UseClient<T>(Func<ClientContext, Task<T>> useClient)
        {
            var startTime = stopwatch.Elapsed;
            T result;
            var context = new ClientContext()
            {
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

        public async Task<ElasticSearchStore> CreateStoreAsync(ElasticSearchStoreConfiguration configuration)
        {
            var store = new ElasticSearchStore(configuration, this);
            await store.InitializeAsync();
            return store;
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

        public ElasticSearchServiceConfiguration(string endpoint)
        {
            Endpoint = endpoint;
        }
    }
}
