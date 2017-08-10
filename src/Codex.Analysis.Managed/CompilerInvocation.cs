using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis.Managed
{
    public class CompilerInvocation
    {
        public string Language { get; internal set; }
        public string CommandLine { get; internal set; }
        public string ProjectFile { get; internal set; }
        public string ProjectDirectory => Path.GetDirectoryName(ProjectFile);

        public CommandLineArguments GetCommandLineArguments()
        {
            var sdkDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            var args = CommandLineParser.SplitCommandLineIntoArguments(CommandLine, removeHashComments: false);
            CommandLineArguments arguments;
            if (Language == LanguageNames.CSharp)
            {
                arguments = CSharpCommandLineParser.Default.Parse(args, ProjectDirectory, sdkDirectory);
            }
            else
            {
                arguments = VisualBasicCommandLineParser.Default.Parse(args, ProjectDirectory, sdkDirectory);
            }

            return arguments;
        }

        public override string ToString()
        {
            return $"{ProjectFile} {((CommandLine != null && CommandLine.Length > 60) ? CommandLine.Substring(0, 60) + "..." : CommandLine)}";
        }
    }
}
