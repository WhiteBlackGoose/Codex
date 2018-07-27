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
    public abstract class InvocationSolutionInfoBuilderBase
    {
        public readonly string SolutionName;
        public AdhocWorkspace Workspace;
        public Dictionary<string, CompilerInvocation> InvocationsByProjectPath = new Dictionary<string, CompilerInvocation>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ProjectInfoBuilder> ProjectInfoByAssemblyNameMap = new ConcurrentDictionary<string, ProjectInfoBuilder>(StringComparer.OrdinalIgnoreCase);
        protected Repo repo;
        private Logger logger;

        public bool HasProjects => ProjectInfoByAssemblyNameMap.Count != 0;

        protected InvocationSolutionInfoBuilderBase(string solutionName, Repo repo)
        {
            this.SolutionName = solutionName;
            this.repo = repo;
            this.logger = repo.AnalysisServices.Logger;

            Workspace = new AdhocWorkspace(DesktopMefHostServices.DefaultServices);
        }

        public void StartLoadProject(CompilerInvocation invocation)
        {
            var projectPath = invocation.ProjectFile;
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

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

            var argsWithoutAnalyzers = args.Where(arg => !IsAnalyzerArg(arg)).ToArray();

            var projectInfo = CommandLineProject.CreateProjectInfo(
                projectName: projectName,
                language: languageName,
                commandLineArgs: argsWithoutAnalyzers,
                projectDirectory: projectDirectory,
                workspace: Workspace);

            projectInfo = projectInfo.WithOutputFilePath(outputPath).WithFilePath(projectPath);
            return projectInfo;
        }

        private bool IsAnalyzerArg(string arg)
        {
            return arg.StartsWith("/a:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/analyzer:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-a:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-analyzer:", StringComparison.OrdinalIgnoreCase);
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

            return SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default, SolutionName, projects);
        }

        private class ProjectInfoBuilder
        {
            public bool HasProjectInfo => ProjectInfo != null;
            public string AssemblyName;
            private ProjectInfo projectInfo;
            public ProjectInfo ProjectInfo
            {
                get
                {
                    return projectInfo;
                }
                set
                {
                    if (projectInfo == null)
                    {
                        projectInfo = value;
                    }
                    else
                    {
                        projectInfo = GetBestProjectInfo(projectInfo, value);
                    }
                }
            }

            private ProjectInfo GetBestProjectInfo(ProjectInfo projectInfo1, ProjectInfo projectInfo2)
            {
                // Heuristic: Project with most documents is the best project info
                if (projectInfo1.Documents.Count > projectInfo2.Documents.Count)
                {
                    return projectInfo1;
                }

                return projectInfo2;
            }

            public ProjectInfoBuilder(string assemblyName)
            {
                AssemblyName = assemblyName;
            }
        }
    }
}
