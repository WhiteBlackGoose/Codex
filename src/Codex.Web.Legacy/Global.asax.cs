using System;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Autofac;
using Autofac.Integration.Mvc;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Legacy.Bridge;
using Codex.ElasticSearch.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage;

namespace WebUI
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            RegisterDependencyInjection();
            //AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void RegisterDependencyInjection()
        {
            var builder = new ContainerBuilder();

            builder.RegisterControllers(typeof(MvcApplication).Assembly);

            // TODO: use a config entry for this
            //builder.RegisterType<Newtonsoft.Json.JsonSerializer>()
            //    .AsSelf()
            //    .SingleInstance();

            //builder.Register(_ => new ElasticsearchStorage("http://localhost:9200", requiresProjectGraph: true))
            //    .As<IStorage>()
            //    .SingleInstance();

            builder.Register(_ => new LegacyElasticSearchCodex(
                new LegacyElasticSearchStoreConfiguration()
                {
                    Endpoint = "http://ddindex:9125"
                }))
                .As<ICodex>()
                .SingleInstance();

            //builder.Register(_ => new ElasticSearchCodex(
            //    new ElasticSearchStoreConfiguration()
            //    {
            //        Prefix = "estest."
            //    },
            //    new ElasticSearchService(new ElasticSearchServiceConfiguration("http://localhost:9200"))))
            //    .As<ICodex>()
            //    .SingleInstance();

            DependencyResolver.SetResolver(new AutofacDependencyResolver(builder.Build()));
        }
    }
}
