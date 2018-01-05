using Autofac;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage;

namespace WebUI
{
    internal class ElasticSearchModule : Module
    {
        public string Endpoint { get; set; }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(_ => new ElasticSearchCodex(
                new ElasticSearchStoreConfiguration(), 
                new ElasticSearchService(new ElasticSearchServiceConfiguration(Endpoint))))
                .As<ICodex>()
                .SingleInstance();
        }
    }
}
