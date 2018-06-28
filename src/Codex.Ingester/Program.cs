using CommandLine;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;
using CommandLine.Text;
using Newtonsoft.Json;
using Codex.Downloader;
using Codex.Application;

namespace Codex.Ingester
{
    class Program
    {
        class Options
        {
            [Option("file", Required = true, HelpText = "The location of a json file containing definition of repo analysis output locations.")]
            public string DefinitionsFile { get; set; }

            [Option("out", Required = true, HelpText = "The temporary location to store analysis outputs before uploading.")]
            public string OutputFolder { get; set; }

            [Option("incremental", HelpText = "Specifies if files should not be downloaded if already present at output location.")]
            public bool Incremental { get; set; }

            [Option("name", Required = true, HelpText = "The name of the repository to create.")]
            public string RepoName { get; set; }

            [Option("es", Required = true, HelpText = "The URL of the elasticsearch instance.")]
            public string ElasticSearchUrl { get; set; }
        }

        static void Main(string[] args)
        {
            new CommandLine.Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Out;
            }).ParseArguments<Options>(args)
                .WithParsed<Options>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<Options>((errs) => HandleParseError(errs));
        }

        private static async Task RunAsync(Options options)
        {
            // Read the json file
            RepoList repoList = JsonConvert.DeserializeObject<RepoList>(File.ReadAllText(options.DefinitionsFile));

            // Download each of the repos to a subfolder with the repo name
            // (maybe also in-proc)

            Directory.CreateDirectory(options.OutputFolder);
            foreach (var repo in repoList.repos)
            {
                var destination = Path.Combine(options.OutputFolder, repo.name + ".zip");
                if (options.Incremental && File.Exists(destination))
                {
                    continue;
                }

                await DownloaderProgram.RunAsync(new DownloaderProgram.VSTSBuildOptions()
                {
                    BuildDefinitionId = repo.id,
                    CollectionUri = repo.url,
                    Destination = destination,
                    ProjectName = repo.project,
                    PersonalAccessToken = GetPersonalAccessToken(options, repo.pat)
                });
            }

            // Run codex.exe (maybe just launch in-proc)
            // specifying the root folder and a mode indicating that
            // they all go into the same partition

            CodexApplication.Ingest(
                options.RepoName,
                options.ElasticSearchUrl,
                options.OutputFolder,
                false);

            // TODO: Maybe have handling to ensure that same code is not uploaded more
            // than once
        }

        private static string GetPersonalAccessToken(Options options, string pat)
        {
            var result = Environment.ExpandEnvironmentVariables(pat);
            return result;
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            var sb = SentenceBuilder.Create();
            foreach (var error in errors)
            {
                if (error.Tag != ErrorType.HelpRequestedError)
                {
                    Console.Error.WriteLine(sb.FormatError(error));
                }
            }
        }

        private static void RunOptionsAndReturnExitCode(Options options)
        {
            RunAsync(options).GetAwaiter().GetResult();
        }
    }
}
