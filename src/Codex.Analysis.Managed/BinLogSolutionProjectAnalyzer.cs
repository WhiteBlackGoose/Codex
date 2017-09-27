using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Managed;
using Codex.Import;
using Codex.MSBuild;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Projects
{
    public class BinLogSolutionProjectAnalyzer : SolutionProjectAnalyzer
    {
        public BinLogSolutionProjectAnalyzer(string[] includedSolutions = null)
            : base(includedSolutions)
        {
        }

        protected override Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile)
        {
            var repo = repoFile.PrimaryProject.Repo;
            var dispatcher = repo.AnalysisServices.TaskDispatcher;
            var services = repo.AnalysisServices;
            var logger = repo.AnalysisServices.Logger;

            var solutionFile = SolutionFile.Parse(new SourceTextReader(SourceText.From(services.ReadAllText(repoFile.FilePath))));

            SolutionInfoBuilder solutionInfo = new SolutionInfoBuilder(repoFile.FilePath, repo);

            foreach (var projectBlock in solutionFile.ProjectBlocks)
            {
                solutionInfo.StartLoadProject(projectBlock.ProjectPath);
            }

            if (solutionInfo.HasProjects)
            {
                repoFile.HasExplicitAnalyzer = true;
            }

            return Task.FromResult(solutionInfo.Build());
        }

        private class SolutionInfoBuilder
        {
            private string solutionPath;
            private string solutionDirectory;
            private AdhocWorkspace workspace;
            public Dictionary<string, CompilerInvocation> InvocationsByProjectPath = new Dictionary<string, CompilerInvocation>(StringComparer.OrdinalIgnoreCase);
            public ConcurrentDictionary<string, ProjectInfoBuilder> ProjectInfoByAssemblyNameMap = new ConcurrentDictionary<string, ProjectInfoBuilder>(StringComparer.OrdinalIgnoreCase);
            private Repo repo;
            private string binLogPath;

            public bool HasProjects => ProjectInfoByAssemblyNameMap.Count != 0;

            public SolutionInfoBuilder(string filePath, Repo repo)
            {
                this.solutionPath = filePath;
                this.solutionDirectory = Path.GetDirectoryName(solutionPath);
                this.repo = repo;
                this.binLogPath = Directory.GetFiles(solutionDirectory, "*.binlog", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault();

                workspace = new AdhocWorkspace(DesktopMefHostServices.DefaultServices);

                Initialize();
            }

            public void Initialize()
            {
                if (binLogPath == null)
                {
                    return;
                }

                foreach (var invocation in BinLogReader.ExtractInvocations(binLogPath))
                {
                    InvocationsByProjectPath[invocation.ProjectFile] = invocation;
                }
            }

            public void StartLoadProject(string projectPath)
            {
                projectPath = Path.GetFullPath(Path.Combine(solutionDirectory, projectPath));
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                if (!InvocationsByProjectPath.TryGetValue(projectPath, out var invocation))
                {
                    return;
                }

                ProjectInfo projectInfo = GetCommandLineProject(projectPath, projectName, invocation.Language, invocation.GetCommandLineArguments());
                var assemblyName = Path.GetFileNameWithoutExtension(projectInfo.OutputFilePath);
                ProjectInfoBuilder info = GetProjectInfo(assemblyName);
                info.ProjectInfo = projectInfo;
            }

            private ProjectInfoBuilder GetProjectInfo(string assemblyName)
            {
                return ProjectInfoByAssemblyNameMap.GetOrAdd(assemblyName, k => new ProjectInfoBuilder(k));
            }

            private ProjectInfo GetCommandLineProject(string projectPath, string projectName, string languageName, string[] args)
            {
                var projectDirectory = Path.GetDirectoryName(projectPath);
                string outputPath;
                if (languageName == LanguageNames.VisualBasic)
                {
                    var vbArgs = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCommandLineParser.Default.Parse(args, projectPath, sdkDirectory: null);
                    outputPath = Path.Combine(vbArgs.OutputDirectory, vbArgs.OutputFileName);
                }
                else
                {
                    var csArgs = Microsoft.CodeAnalysis.CSharp.CSharpCommandLineParser.Default.Parse(args, projectPath, sdkDirectory: null);
                    outputPath = Path.Combine(csArgs.OutputDirectory, csArgs.OutputFileName);
                }

                var projectInfo = CommandLineProject.CreateProjectInfo(
                    projectName: projectName,
                    language: languageName,
                    commandLineArgs: args,
                    projectDirectory: projectDirectory,
                    workspace: workspace);

                projectInfo = projectInfo.WithOutputFilePath(outputPath).WithFilePath(projectPath);
                return projectInfo;
            }

            internal SolutionInfo Build()
            {
                List<ProjectInfo> projects = new List<ProjectInfo>();
                foreach (var project in ProjectInfoByAssemblyNameMap.Values)
                {
                    if (project.HasProjectInfo)
                    {
                        projects.Add(project.ProjectInfo);
                    }
                }

                return SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, solutionPath, projects);
            }
        }

        private class ProjectInfoBuilder
        {
            public bool HasProjectInfo => ProjectInfo != null;
            public ProjectInfo ProjectInfo;
            public string AssemblyName;

            public ProjectInfoBuilder(string assemblyName)
            {
                AssemblyName = assemblyName;
            }
        }
    }
}
