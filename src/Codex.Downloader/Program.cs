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

            [Option("id", HelpText = "The Id of the build definition.")]
            public int BuildDefinitionId { get; set; }
        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<VSTSBuildOptions>(args)
                .WithParsed<VSTSBuildOptions>(opts => RunOptionsAndReturnExitCode(opts))
                .WithNotParsed<VSTSBuildOptions>((errs) => HandleParseError(errs));
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
            BuildHttpClient client;
            //Microsoft.VisualStudio.Services.WebApi.client
        }
    }
}
