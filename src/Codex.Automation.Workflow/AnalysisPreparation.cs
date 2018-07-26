﻿using System;
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

        public AnalysisPreparation(Arguments arguments)
        {
            this.arguments = arguments;
        }

        public void Run()
        {
            arguments.RepoName = GetRepoName();

            bool successfullyCloned = Invoke("git.exe", "clone", arguments.CodexRepoUrl, arguments.SourcesDirectory);
            if (!successfullyCloned)
            {
                throw new Exception($"Failed to clone {arguments.CodexRepoUrl}");
            }

            string[] solutions = EnumerateSolutions();

            // TODO: Rewrite projects?

            TryRestore(solutions);

            TryBuild(solutions);

            foreach (var binLogPath in binLogPaths)
            {
                arguments.AdditionalCodexArguments += $" -bld {QuoteIfNecessary(binLogPath)} "; 
            }
        }

        private string GetRepoName()
        {
            var repoName = arguments.CodexRepoUrl;
            repoName = repoName.TrimEnd('/');
            var lastSlashIndex = repoName.LastIndexOf('/');
            if (lastSlashIndex > 0)
            {
                repoName = repoName.Substring(0, lastSlashIndex);
            }

            return repoName;
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
            var binLogPath = $@"{arguments.CodexOutputRoot}\binlogs\{binlogName}.binlog";
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
