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
            throw new NotImplementedException();
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
        }
    }
}
