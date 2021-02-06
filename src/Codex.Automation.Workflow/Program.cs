using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Automation.Workflow
{
    using static Helpers;

    class Arguments
    {
        public string AdditionalCodexArguments;
        public string CodexOutputRoot;
        public string CodexRepoUrl;
        public string SourcesDirectory;
        public string RepoName;
        public string ElasticSearchUrl;
        public string JsonFilePath;
        public bool NoClone;
        public Dictionary<string, string> PersonalAccessTokens = new Dictionary<string, string>();
    }

    enum Mode
    {
        Prepare = 1 << 0,
        UploadOnly = 1 << 1,
        IngestOnly = 1 << 2,
        // For historical reasons, analyze only also includes upload
        AnalyzeOnly = 1 << 3 | UploadOnly,
        BuildOnly = 1 << 4,
        GC = 1 << 5 | Prepare,
        FullAnalyze = Prepare | AnalyzeOnly,
        Ingest = Prepare | IngestOnly,
        Upload = Prepare | UploadOnly,
        Build = Prepare | BuildOnly | FullAnalyze | Upload,
    }

    class Program
    {
        private static Arguments ParseArguments(string[] args)
        {
            Arguments newArgs = new Arguments();
            foreach (string arg in args)
            {
                string argValue;
                bool switchValue;
                if (MatchArg(arg, "SourcesDirectory", out argValue))
                {
                    newArgs.SourcesDirectory = argValue;
                }
                else if (MatchArg(arg, "AdditionalCodexArguments", out argValue))
                {
                    newArgs.AdditionalCodexArguments = newArgs.AdditionalCodexArguments != null ?
                        string.Join(" ", newArgs.AdditionalCodexArguments, argValue) :
                        argValue;
                }
                else if (MatchArg(arg, "CodexOutputRoot", out argValue))
                {
                    newArgs.CodexOutputRoot = argValue;
                }
                else if (MatchSwitch(arg, "NoClone", out switchValue))
                {
                    newArgs.NoClone = switchValue;
                }
                else if (MatchArg(arg, "CodexRepoUrl", out argValue))
                {
                    newArgs.CodexRepoUrl = argValue;
                }
                else if (MatchArg(arg, "RepoName", out argValue))
                {
                    newArgs.RepoName = argValue;
                }
                else if (MatchArg(arg, "ElasticSearchUrl", out argValue))
                {
                    newArgs.ElasticSearchUrl = argValue;
                }
                else if (MatchArg(arg, "JsonFilePath", out argValue))
                {
                    newArgs.JsonFilePath = argValue;
                }
                else if (MatchArg(arg, "Pat", out argValue))
                {
                    var pair = argValue.Split('=');
                    Console.WriteLine($"Adding PAT: '{pair[0]}'='{string.Empty.PadRight(Math.Max(3, pair[1].Length), '*')}'");
                    newArgs.PersonalAccessTokens[pair[0]] = pair[1];
                }
                else if (MatchArg(arg, "PrintEnv", out argValue))
                {
                    Console.WriteLine($"Environment Variables:");

                    foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
                    {
                        Console.WriteLine($"{envVar.Key}={envVar.Value}");
                    }

                    Console.WriteLine($"Done printing environment variables.");
                }
                else
                {
                    throw new ArgumentException("Invalid Arguments: " + arg);
                }
            }
            return newArgs;
        }

        private static bool MatchArg(string arg, string argName, out string argValue)
        {
            if (arg.StartsWith($"/{argName}:", StringComparison.OrdinalIgnoreCase))
            {
                argValue = arg.Substring(argName.Length + 2);
                return true;
            }
            argValue = null;
            return false;
        }

        private static bool MatchSwitch(string arg, string argName, out bool argValue)
        {
            var prefix = $"/{argName}";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Length == prefix.Length)
                {
                    argValue = true;
                    return true;
                }
                else if (arg.Length == (prefix.Length + 1))
                {
                    switch (arg[prefix.Length])
                    {
                        case '-':
                            argValue = false;
                            return true;
                        case '+':
                            argValue = true;
                            return true;
                    }
                }
            }
            argValue = false;
            return false;
        }

        private static Mode GetMode(ref string[] args)
        {
            if (args[0].StartsWith("/"))
            {
                // No mode specified. Use default mode.
                return Mode.FullAnalyze;
            }
            else
            {
                var modeArgument = args[0];
                args = args.Skip(1).ToArray();

                if (!Enum.TryParse<Mode>(modeArgument, ignoreCase: true, result: out var mode))
                {
                    throw new ArgumentException("Invalid mode: " + modeArgument);
                }

                return mode;
            }
        }

        private static bool HasModeFlag(Mode mode, Mode flag)
        {
            return (mode & flag) == flag;
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                // TODO: Add help text
                Console.WriteLine("No arguments specified.");
                return;
            }

            Mode mode = GetMode(ref args);
            bool success = true;

            Arguments arguments = ParseArguments(args);
            if (string.IsNullOrEmpty(arguments.RepoName))
            {
                arguments.RepoName = GetRepoName(arguments);
            }

            Console.WriteLine("##vso[build.addbuildtag]CodexEnabled");

            string codexBinDirectory = Path.Combine(arguments.CodexOutputRoot, "bin");
            string binlogDirectory = Path.Combine(arguments.CodexOutputRoot, "binlogs");
            string analysisOutputDirectory = Path.Combine(arguments.CodexOutputRoot, "store");
            string analysisArguments = string.Join(" ",
                    "index",
                    "-save",
                    analysisOutputDirectory,
                    "-p",
                    arguments.SourcesDirectory,
                    "-repoUrl",
                    arguments.CodexRepoUrl,
                    "-n",
                    arguments.RepoName,
                    arguments.AdditionalCodexArguments,
                    "-bld",
                    binlogDirectory);
            string executablePath = Path.Combine(codexBinDirectory, "Codex.exe");

            if (HasModeFlag(mode, Mode.Prepare))
            {
                // download files
                var client = new WebClient();
                string downloadDirectory = Path.Combine(arguments.CodexOutputRoot, "zip");
                string zipPath = Path.Combine(downloadDirectory, "Codex.zip");
                Directory.CreateDirectory(downloadDirectory);

                Console.WriteLine("Downloading Files...");
                client.DownloadFile("https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.zip", zipPath);

                // zip/unzip files
                Console.WriteLine("Creating Directories");
                if (Directory.Exists(codexBinDirectory))
                {
                    Directory.Delete(codexBinDirectory, true);
                }

                Directory.CreateDirectory(codexBinDirectory);
                Console.WriteLine("Extracting Zip Files");
                ZipFile.ExtractToDirectory(zipPath, codexBinDirectory); // TODO: doesnt work if the directory already contains files

                Console.WriteLine($"##vso[task.setvariable variable=CodexBinDir;]{codexBinDirectory}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexAnalysisOutDir;]{analysisOutputDirectory}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexExePath;]{executablePath}");
                Console.WriteLine($"##vso[task.setvariable variable=CodexAnalysisArguments;]{analysisArguments}");

                if (!string.IsNullOrEmpty(arguments.RepoName))
                {
                    Console.WriteLine($"##vso[task.setvariable variable=CodexRepoName;]{arguments.RepoName}");
                }
            }

            if (HasModeFlag(mode, Mode.GC))
            {
                // run exe
                var gcArguments = $"gc --es {arguments.ElasticSearchUrl}";
                success &= RunProcess(executablePath, gcArguments);
            }

            if (HasModeFlag(mode, Mode.BuildOnly))
            {
                var analysisPreparation = new AnalysisPreparation(arguments, binlogDirectory);
                analysisPreparation.Run();
            }

            if (HasModeFlag(mode, Mode.AnalyzeOnly))
            {
                // run exe
                success &= RunProcess(executablePath, analysisArguments);
            }

            if (HasModeFlag(mode, Mode.UploadOnly))
            {
                if (success)
                {
                    // get json files and zip
                    Console.WriteLine("Zipping analysis files.");

                    string analysisOutputZip = analysisOutputDirectory + ".zip";
                    ZipFile.CreateFromDirectory(analysisOutputDirectory, analysisOutputZip);

                    // publish to a vsts build
                    Console.WriteLine("Publishing to Build");
                    Console.WriteLine($"##vso[artifact.upload artifactname=CodexOutputs;]{analysisOutputZip}");
                    Console.WriteLine("##vso[build.addbuildtag]CodexOutputs");
                }
                else
                {
                    Console.WriteLine("Analysis failed. Skipping public of analysis files.");
                }
            }

            if (HasModeFlag(mode, Mode.IngestOnly))
            {
                string ingesterExecutablePath = Path.Combine(codexBinDirectory, "Codex.Ingester.exe");
                string storeFolder = Path.Combine(arguments.CodexOutputRoot, "store");
                success &= RunProcess(
                    exePath: ingesterExecutablePath,
                    arguments: string.Join(" ",
                        "--file",
                        arguments.JsonFilePath,
                        "--out",
                        storeFolder,
                        "--es",
                        arguments.ElasticSearchUrl,
                        "--name",
                        arguments.RepoName), 
                    envVars: arguments.PersonalAccessTokens);
            }

            if (!success)
            {
                Console.WriteLine("##vso[task.complete result=Failed;]DONE");
            }
        }

        private static bool RunProcess(string exePath, string arguments, IDictionary<string, string> envVars = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                arguments = string.Join(" ", QuoteIfNecessary(exePath), arguments);
                exePath = "mono";
            }

            Console.WriteLine("Running Process:");
            Console.WriteLine(exePath + " " + arguments);

            var process = Process.Start(new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = false
            }
            .With(info =>
            {
                if (envVars != null)
                {
                    foreach (var kvp in envVars)
                    {
                        info.EnvironmentVariables[kvp.Key] = kvp.Value;
                    }
                }
            }));

            process.WaitForExit();
            return process.ExitCode == 0;
        }

        private static string GetRepoName(Arguments arguments)
        {
            var repoName = arguments.CodexRepoUrl;

            if (!string.IsNullOrEmpty(repoName))
            {
                repoName = repoName.TrimEnd('/');
                var lastSlashIndex = repoName.LastIndexOf('/');
                if (lastSlashIndex > 0)
                {
                    repoName = repoName.Substring(lastSlashIndex + 1);
                }
            }

            return repoName;
        }
    }
}
