using CommandLine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.IO.Compression;
using CommandLine.Text;

namespace Codex.Rewriter
{
    public class RewriterProgram
    {
        public class Options
        {
            [Option("assembly", Required = true, HelpText = "The assembly to rewrite.")]
            public string Assembly { get; set; }

            [Option('o', "out", Required = true, HelpText = "The output file path of the rewritten assembly.")]
            public string OutputFilePath { get; set; }

            public bool Preview { get; set; }
        }

        public static void Main(string[] args)
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

        public static async Task RunAsync(Options options)
        {
           
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
