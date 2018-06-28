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

namespace Codex.Downloader
{
    public class DownloaderProgram
    {
        public class VSTSBuildOptions
        {
            [Option("uri", Required = true, HelpText = "The URI of the project collection.")]
            public string CollectionUri { get; set; }

            [Option('p', "project", Required = true, HelpText = "The name of the VSTS project.")]
            public string ProjectName { get; set; }

            [Option("id", Required = true, HelpText = "The Id of the build definition.")]
            public int BuildDefinitionId { get; set; }

            [Option('o', "out", Required = true, HelpText = "The output location to store the index artifact zip file.")]
            public string Destination { get; set; }

            [Option("pat", Required = true, HelpText = "The personal access token used to access the account.")]
            public string PersonalAccessToken { get; set; }
        }

        public static void Main(string[] args)
        {
            new CommandLine.Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Out;
            }).ParseArguments<VSTSBuildOptions>(args)
                .WithParsed<VSTSBuildOptions>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<VSTSBuildOptions>((errs) => HandleParseError(errs));
        }

        public static async Task RunAsync(VSTSBuildOptions options)
        {
            string project = options.ProjectName;
            var tempFilePath = Path.GetTempFileName();
            var destination = options.Destination;
            string collectionUri = options.CollectionUri;

             BuildHttpClient client = new BuildHttpClient(
                new Uri(collectionUri),
                new VssBasicCredential(string.Empty, options.PersonalAccessToken));

            Console.WriteLine($"Getting build definition: {options.BuildDefinitionId}");

            var definition = await client.GetDefinitionAsync(
                project: project,
                definitionId: options.BuildDefinitionId);

            if (definition == null)
            {
                Console.Error.WriteLine("Unable to find build definition");
                return;
            }

            var projectId = definition.Project.Id;

            Console.WriteLine($"Found build {definition.Name} in project {definition.Project.Name}");

            var lastBuild = (await client.GetBuildsAsync(
                project: projectId,
                definitions: new[] { definition.Id },
                tagFilters: new[] { "CodexOutputs" },
                resultFilter: BuildResult.Succeeded | BuildResult.PartiallySucceeded,
                queryOrder: BuildQueryOrder.FinishTimeDescending,
                top: 1)).FirstOrDefault();

            if (lastBuild == null)
            {
                Console.Error.WriteLine("Could not find successful build.");
                return;
            }

            Console.WriteLine($"Found build: {lastBuild.BuildNumber} (id: {lastBuild.Id})");

            destination = Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

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
            var sb = SentenceBuilder.Create();
            foreach (var error in errors)
            {
                if (error.Tag != ErrorType.HelpRequestedError)
                {
                    Console.Error.WriteLine(sb.FormatError(error));
                }
            }
        }

        private static void RunOptionsAndReturnExitCode(VSTSBuildOptions options)
        {
            RunAsync(options).GetAwaiter().GetResult();
        }
    }
}
