using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Managed;
using Codex.Import;
using Codex.Logging;
using Codex.MSBuild;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Projects
{
    public class CompilerArgumentsProjectAnalyzer : RepoProjectAnalyzerBase
    {
        private readonly string[] argsFiles;
        public bool RequireProjectFilesExist { get; set; }

        public CompilerArgumentsProjectAnalyzer(params string[] argsFiles)
        {
            this.argsFiles = argsFiles;
        }

        public override void CreateProjects(Repo repo)
        {
            if (argsFiles.Length != 0)
            {
                SolutionInfoBuilder builder = new SolutionInfoBuilder(argsFiles, repo);
                if (builder.HasProjects)
                {
                    SolutionProjectAnalyzer.AddSolutionProjects
                        (repo,
                        () => Task.FromResult(builder.Build()),
                        workspace: builder.Workspace,
                        requireProjectExists: RequireProjectFilesExist,
                        solutionName: builder.SolutionName);
                }
            }
        }

        private class SolutionInfoBuilder : InvocationSolutionInfoBuilderBase
        {
            private string[] argsFiles;

            public SolutionInfoBuilder(string[] argsFiles, Repo repo)
                : base(argsFiles.First(), repo)
            {
                this.argsFiles = argsFiles;
                Initialize();
            }

            public void Initialize()
            {
                foreach (var argsFile in argsFiles)
                {
                    if (Directory.Exists(argsFile))
                    {
                        foreach (var file in Directory.GetFiles(argsFile, "*.args.txt", SearchOption.AllDirectories))
                        {
                            ReadArgsFile(file);
                        }
                    }
                    else
                    {
                        ReadArgsFile(argsFile);
                    }
                }
            }

            private void ReadArgsFile(string argsFile)
            {
                const string ProjectFilePrefix = "Project=";
                var args = File.ReadAllLines(argsFile);
                var argsFileName = Path.GetFileName(argsFile).ToLower();
                var languageName = argsFileName == "csc.args.txt" ? LanguageNames.CSharp : LanguageNames.VisualBasic;
                var projectFile = argsFile;
                int startIndex = 0;
                if (args[0].StartsWith(ProjectFilePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    projectFile = args[0].Substring(ProjectFilePrefix.Length);
                    startIndex++;
                }

                repo.AnalysisServices.Logger.LogMessage($"Reading args file '{argsFile}' for project '{projectFile ?? string.Empty}'");

                var invocation = new CompilerInvocation()
                {
                    Language = languageName,
                    ProjectFile = projectFile,
                    CommandLineArguments = args.Skip(startIndex).ToArray()
                };

                InvocationsByProjectPath[invocation.ProjectFile] = invocation;
                StartLoadProject(invocation);
            }
        }
    }
}
