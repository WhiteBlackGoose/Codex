using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Automation.Workflow
{
    class Arguments
    {
        public string AdditionalCodexArguments;
        public string CodexOutputRoot;
        public string CodexRepoUrl;
        public string SourcesDirectory;
        public string RepoName;
        public string ElasticSearchUrl;
        public string JsonFilePath;
    }

    enum Mode
    {
        Prepare = 1 << 0,
        AnalyzeOnly = 1 << 1,
        UploadOnly = 1 << 2,
        FullAnalyze = Prepare | AnalyzeOnly,
        Upload = Prepare | UploadOnly
    }

    class Program
    {
        private static Arguments ParseArguments(string[] args)
        {
            Arguments newArgs = new Arguments();
            foreach (string arg in args)
            {
                string argValue;
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

            Arguments arguments = ParseArguments(args);

            string codexBinDirectory = Path.Combine(arguments.CodexOutputRoot, "bin");
            string analysisOutputDirectory = Path.Combine(arguments.CodexOutputRoot, "store");

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
            }

            if (HasModeFlag(mode, Mode.AnalyzeOnly))
            {
                // run exe
                Console.WriteLine("Running Process");
                string executablePath = Path.Combine(codexBinDirectory, "Codex.exe");
                Process runExe = Process.Start(new ProcessStartInfo(executablePath, string.Join(" ",
                    "index",
                    "-save",
                    analysisOutputDirectory,
                    "-p",
                    arguments.SourcesDirectory,
                    "-repoUrl",
                    arguments.CodexRepoUrl,
                    "-n",
                    arguments.RepoName,
                    arguments.AdditionalCodexArguments))
                {
                    UseShellExecute = false
                });
                runExe.WaitForExit();

                // get json files and zip
                Console.WriteLine("Zipping JSON files");

                string analysisOutputZip = analysisOutputDirectory + ".zip";
                ZipFile.CreateFromDirectory(analysisOutputDirectory, analysisOutputZip);

                // publish to a vsts build
                Console.WriteLine("Publishing to Build");
                Console.WriteLine($"##vso[artifact.upload artifactname=CodexOutputs;]{analysisOutputZip}");
                Console.WriteLine("##vso[build.addbuildtag]CodexOutputs");
            }

            if (HasModeFlag(mode, Mode.UploadOnly))
            {
                string executablePath = Path.Combine(codexBinDirectory, "Codex.Ingester.exe");
                string storeFolder = Path.Combine(arguments.CodexOutputRoot, "store");
                Process runExe = Process.Start(new ProcessStartInfo(executablePath, string.Join(" ",
                    "--file",
                    arguments.JsonFilePath,
                    "--out",
                    storeFolder,
                    "--es",
                    arguments.ElasticSearchUrl,
                    "--name",
                    arguments.RepoName))
                {
                    UseShellExecute = false
                });
                
            }
        }
    }
}
