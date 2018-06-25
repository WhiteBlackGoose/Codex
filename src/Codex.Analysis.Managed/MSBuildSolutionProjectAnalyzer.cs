using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codex.Import;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Codex.Analysis.Projects
{
    public class MSBuildSolutionProjectAnalyzer : SolutionProjectAnalyzer
    {
        MSBuildProjectLoader loader;

        public MSBuildSolutionProjectAnalyzer(string[] includedSolutions = null)
            : base(includedSolutions)
        {
            var workspace = MSBuildWorkspace.Create();
            
            workspace.WorkspaceFailed += Workspace_WorkspaceFailed;
            var propertiesOpt = ImmutableDictionary<string, string>.Empty;

            // Explicitly add "CheckForSystemRuntimeDependency = true" property to correctly resolve facade references.
            // See https://github.com/dotnet/roslyn/issues/560
            propertiesOpt = propertiesOpt.Add("CheckForSystemRuntimeDependency", "true");
            propertiesOpt = propertiesOpt.Add("VisualStudioVersion", "15.0");
            propertiesOpt = propertiesOpt.Add("AlwaysCompileMarkupFilesInSeparateDomain", "false");

            loader = new MSBuildProjectLoader(workspace, propertiesOpt)
            {
                SkipUnrecognizedProjects = true,
            };
        }

        private void Workspace_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            //throw new Exception(e.Diagnostic.Message);
        }

        protected override Task<SolutionInfo> GetSolutionInfoAsync(RepoFile repoFile)
        {
            return loader.LoadSolutionInfoAsync(repoFile.FilePath);
        }
    }
}
