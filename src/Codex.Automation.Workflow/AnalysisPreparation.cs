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
    using static Helpers;

    internal class AnalysisPreparation
    {
        private readonly Arguments arguments;
        private readonly string MsBuildPath = "msbuild";
        private readonly string DotNetPath = "dotnet";
        private readonly string NugetPath = "nuget";

        private readonly string binlogDirectory;

        public AnalysisPreparation(Arguments arguments, string binlogDirectory)
        {
            this.arguments = arguments;
            this.binlogDirectory = binlogDirectory;
        }

        public void Run()
        {
            bool successfullyCloned = arguments.NoClone 
                || Invoke("git.exe", "clone", arguments.CodexRepoUrl, arguments.SourcesDirectory);
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

            if (Invoke(MsBuildPath, $@"/bl:{binlogDirectory}\{binlogName}.binlog", solution))
            {
                return;
            }

            Invoke(DotNetPath, "build", $@"/bl:{binlogDirectory}\{binlogName}.dn.binlog", solution);
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
    }
}
