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

namespace Codex.Downloader
{
    class Program
    {
        class VSTSBuildOptions
        {
            [Option("uri", Required = true, HelpText = "The URI of the project collection.")]
            public string CollectionUri { get; set; }

            [Option('n', "name", Required = true, HelpText = "The name of the build definition.")]
            public string BuildDefinitionName { get; set; }

            [Option('p', "project", Required = true, HelpText = "The name of the VSTS project.")]
            public string ProjectName { get; set; }

            [Option("id", HelpText = "The Id of the build definition.")]
            public int BuildDefinitionId { get; set; }

            [Option("out", Required = true, HelpText = "The output location to store the index artifact zip file.")]
            public string Destination { get; set; }

            [Option("pat", Required = true, HelpText = "The personal access token used to access the account.")]
            public string PersonalAccessToken { get; set; }
        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<VSTSBuildOptions>(args)
                .WithParsed<VSTSBuildOptions>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<VSTSBuildOptions>((errs) => HandleParseError(errs));
        }

        private static async Task RunAsync(VSTSBuildOptions options)
        {
            string project = options.ProjectName;
            var tempFilePath = Path.GetTempFileName();
            var destination = options.Destination;
            string buildDefinitionName = options.BuildDefinitionName;
            string collectionUri = options.CollectionUri;

            BuildHttpClient client = new BuildHttpClient(
                new Uri(collectionUri),
                new VssBasicCredential(string.Empty, options.PersonalAccessToken));

            var definitions = await client.GetDefinitionsAsync(
                project: project,
                name: buildDefinitionName);

            if (definitions.Count == 0)
            {
                Console.Error.WriteLine("Unable to find build definition");
                return;
            }

            var definition = definitions.Select(bd => bd.Id).Take(1).ToArray();
            var projectId = definitions.First().Project.Id;

            var lastBuild = (await client.GetBuildsAsync(
                project: projectId,
                definitions: definition,
                resultFilter: BuildResult.Succeeded | BuildResult.PartiallySucceeded,
                queryOrder: BuildQueryOrder.FinishTimeDescending,
                top: 1)).FirstOrDefault();

            if (lastBuild == null)
            {
                Console.Error.WriteLine("Could not find successful build.");
                return;
            }

            Console.WriteLine($"Found build: {lastBuild.BuildNumber} (id: {lastBuild.Id})");

            using (var tempStream = new FileStream(tempFilePath, 
                FileMode.Create, 
                FileAccess.ReadWrite, 
                FileShare.Delete, 64 << 10, 
                FileOptions.DeleteOnClose))
            using (var stream = await client.GetArtifactContentZipAsync(projectId, lastBuild.Id, artifactName: "CodexOutputs"))
            {
                stream.CopyTo(tempStream);
                tempStream.Position = 0;

                using (var archive = new ZipArchive(tempStream, ZipArchiveMode.Read))
                {
                    var entry = archive.Entries.Where(e => e.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).First();
                    using (var entryStream = entry.Open())
                    using (var destinationStream = File.Open(destination, FileMode.Create))
                    {
                        entryStream.CopyTo(destinationStream);
                    }
                }
            }
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(error);
            }
        }

        private static void RunOptionsAndReturnExitCode(VSTSBuildOptions options)
        {
            RunAsync(options).GetAwaiter().GetResult();
        }
    }
}
