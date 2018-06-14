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
                    newArgs.AdditionalCodexArguments = argValue;
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

        static void Main(string[] args)
        {
            Arguments arguments = ParseArguments(args);

            // download files
            var client = new WebClient();
            string downloadDirectory = Path.Combine(arguments.CodexOutputRoot, "zip");
            string zipPath = Path.Combine(downloadDirectory, "Codex.zip");
            Directory.CreateDirectory(downloadDirectory);

            Console.WriteLine("Downloading Files...");
            client.DownloadFile("https://github.com/Ref12/Codex/releases/download/latest-prerel/Codex.zip", zipPath);

            // zip/unzip files
            Console.WriteLine("Creating Directories");
            string codexBin = Path.Combine(arguments.CodexOutputRoot, "bin");
            Directory.CreateDirectory(codexBin);
            Console.WriteLine("Extracting Zip Files");
            ZipFile.ExtractToDirectory(zipPath, codexBin); // TODO doesnt work if the directory already exists?

            // run exe
            Console.WriteLine("Running Process");
            string executablePath = Path.Combine(codexBin, "Codex.exe");
            string analysisOutputDirectory = Path.Combine(arguments.CodexOutputRoot, "store");
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
    }
}
