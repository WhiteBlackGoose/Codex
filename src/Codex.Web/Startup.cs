using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Codex.Sdk.Search;
using Codex.ElasticSearch.Search;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Codex.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(o => o.Conventions.Add(new CodexServiceApplicationModelConvention()));
            services.AddSingleton<ICodex>(new ElasticSearchCodex(new ElasticSearch.ElasticSearchStoreConfiguration()
            {
                Prefix = "apptest"
            }, new ElasticSearch.ElasticSearchService(new ElasticSearch.ElasticSearchServiceConfiguration("http://localhost:9200"))));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors(
                options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().AllowCredentials());

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
