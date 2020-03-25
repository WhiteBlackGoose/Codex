using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
#if BRIDGE
using Codex.View.Web;
#else
using Codex.ElasticSearch.Search;
#endif
namespace Codex.View
{
    partial class App
    {

        public App()
        {
            Styles.Initialize();
#if BRIDGE
            // TODO: This should be configurable through build properties somehow
            CodexProvider.Instance = new WebApiCodex("http://localhost:9491/api/codex/");
#else
            // TODO: This should be configurable through build properties somehow
            CodexProvider.Instance = new ElasticSearchCodex(new ElasticSearch.ElasticSearchStoreConfiguration()
            {
                Prefix = "apptest"
            }, new ElasticSearch.ElasticSearchService(new ElasticSearch.ElasticSearchServiceConfiguration("http://localhost:9200")));
#endif
        }
    }
}
