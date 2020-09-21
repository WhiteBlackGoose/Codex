using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Codex.ElasticSearch;
using Codex.ElasticSearch.Legacy.Bridge;
using Codex.ElasticSearch.Search;
using Codex.Lucene.Search;
using Codex.Sdk.Search;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Codex.Web.Mvc
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
            string elasticSearchEndpoint = Configuration["ES_ENDPOINT"] ?? "http://localhost:9200";
            Console.WriteLine($"ES Endpoint: {elasticSearchEndpoint}");
            if (Configuration["START_ES"] == "1")
            {
                StartElasticSearch();
            }

            services.AddRazorPages();

            var lucenePath = Configuration["LUCENE_PATH"];
            Console.WriteLine($"Lucene Path: {lucenePath}");
            if (!string.IsNullOrEmpty(lucenePath))
            {
                services.Add(ServiceDescriptor.Singleton<ICodex>(new LuceneCodex
                (
                    new LuceneConfiguration(lucenePath)
                )));
            }
            else
            {
                var useCommitModel = Configuration["USE_COMMITMODEL"];
                if (useCommitModel != "1")
                {
                    services.Add(ServiceDescriptor.Singleton<ICodex>(_ => new LegacyElasticSearchCodex(
                        new LegacyElasticSearchStoreConfiguration()
                        {
                            Endpoint = elasticSearchEndpoint
                        })));
                }
                else
                {
                    services.Add(ServiceDescriptor.Singleton<ICodex>(_ =>
                    {
                        ElasticSearchStoreConfiguration configuration = new ElasticSearchStoreConfiguration()
                        {
                            CreateIndices = false,
                            ShardCount = 1,
                            Prefix = "test."
                        };

                        ElasticSearchService service = new ElasticSearchService(new ElasticSearchServiceConfiguration(elasticSearchEndpoint));

                        return new ElasticSearchCodex(configuration, service);
                    }));
                }
            }
        }

        public void StartElasticSearch()
        {
            Console.WriteLine("Drives:");
            Console.WriteLine(string.Join(Environment.NewLine, DriveInfo.GetDrives().Select(d => $"Drive '{d.Name}'")));

            VolumeFunctions.DefineDosDevice(0, "G:", @"C:\ext\");

            Console.WriteLine("Drives:");
            Console.WriteLine(string.Join(Environment.NewLine, DriveInfo.GetDrives().Select(d => $"Drive '{d.Name}'")));

            string testPath = @"G:\es\bin\test.txt";
            File.WriteAllText(testPath, "Wrote this text to a file");
            Console.WriteLine($"Wrote '{testPath}': {File.ReadAllText(testPath)}");

            string elasticsearchPath = @"G:\es\bin\elasticsearch.bat";
            Console.WriteLine($"Starting ElasticSearch '{elasticsearchPath}'");
            Process.Start(new ProcessStartInfo("cmd.exe", $@"/C {elasticsearchPath}")
            {
                EnvironmentVariables =
                {
                    { "JAVA_HOME", @"G:\jdk" }
                }
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Use(async (context, next) =>
            {
                await next.Invoke();
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapControllerRoute(
                    name: "Repos",
                    pattern: "repos/{repoName}/{controller=Home}/{action=Index}/{id?}");
            });
        }
    }

    public class VolumeFunctions
    {
        [DllImport("kernel32.dll")]
        internal static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string lpTargetPath);
    }
}
