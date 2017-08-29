using Codex.Framework.Types;
using Nest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    class ElasticSearchService
    {
        private ElasticClient client;
        private Stopwatch stopwatch = Stopwatch.StartNew();

        public ElasticSearchService(ElasticSearchServiceConfiguration configuration)
        {

        }

        public async Task<ElasticSearchResponse<T>> UseClient<T>(Func<ElasticClient, Task<T>> useClient)
        {
            var startTime = stopwatch.Elapsed;
            T result;

            try
            {
                result = await useClient(this.client);
            }
            catch
            {

            }

            var elapsed = stopwatch.Elapsed - startTime;

            throw new NotImplementedException();
        }

        public ElasticSearchStore CreateStore(ElasticSearchStoreConfiguration configuration)
        {
            throw new NotImplementedException();
        }

    }

    class ElasticSearchResponse<T>
    {

    }

    class ElasticSearchServiceConfiguration
    {

    }
}
