using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Automation.Workflow
{
    internal class AnalysisPreparation
    {
        private readonly Arguments arguments;
        private readonly string MsBuildPath = "msbuild";
        private readonly string DotNetPath = "dotnet";
        private readonly string NugetPath = "nuget";

        private readonly List<string> binLogPaths = new List<string>();
        private readonly string binlogDirectory;

        public AnalysisPreparation(Arguments arguments, string binlogDirectory)
        {
            this.arguments = arguments;
            this.binlogDirectory = binlogDirectory;
        }

        public void Run()
        {
            bool successfullyCloned = Invoke("git.exe", "clone", arguments.CodexRepoUrl, arguments.SourcesDirectory);
            if (!successfullyCloned)
            {
                throw new Exception($"Failed to clone {arguments.CodexRepoUrl}");
            }

            string[] solutions = EnumerateSolutions();

            // TODO: Rewrite projects?

            TryRestore(solutions);

            TryBuild(solutions);
        }

        private void TryBuild(string[] solutions)
        {
            foreach (var solution in solutions)
            {
                TryBuild(solution);
            }
        }

        private void TryBuild(string solution)
        {
            Log(solution);

            var binlogName = ComputeBinLogName(solution);
            var binLogPath = $@"{binlogDirectory}\{binlogName}.binlog";
            binLogPaths.Add(binLogPath);

            Invoke(MsBuildPath, $"/bl:{binLogPath}", solution);
        }

        private string ComputeBinLogName(string solution)
        {
            return $"{Path.GetFileNameWithoutExtension(solution)}.{solution.GetHashCode()}";
        }

        private void TryRestore(string[] solutions)
        {
            foreach (var solution in solutions)
            {
                TryRestore(solution);
            }
        }

        private void TryRestore(string solution)
        {
            Log(solution);

            Invoke(MsBuildPath, "/t:Restore", solution);

            Invoke(DotNetPath, "restore", solution);

            Invoke(NugetPath, "restore", solution);
        }

        private string[] EnumerateSolutions()
        {
            return Directory.GetFiles(arguments.SourcesDirectory, "*.sln", SearchOption.AllDirectories);
        }

        public void Log(string message, [CallerMemberName]string method = null)
        {
            Console.WriteLine($"{method}: {message}");
        }

        public bool Invoke(string processExe, params string[] arguments)
        {
            var processArgs = string.Join(" ", arguments.Select(QuoteIfNecessary));
            Log($"Running: {processExe} {processArgs}");

            try
            {
                var process = Process.Start(new ProcessStartInfo(processExe, processArgs)
                {
                    UseShellExecute = false
                });

                process.WaitForExit();
                Log($"Run completed with exit code '{process.ExitTime}': {processExe} {processArgs}");

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return false;
            }
        }

        private string QuoteIfNecessary(string arg)
        {
            return arg.Contains(" ") ? $"\"{arg}\"" : arg;
        }
    }
}
