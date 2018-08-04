using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.Analysis.Files;
using Codex.Analysis.FileSystems;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Utilities;
using static Codex.Utilities.TaskUtilities;

namespace Codex.Analysis
{

    public class RepoProjectAnalyzerBase : RepoProjectAnalyzer
    {
    }

    public class NullRepoProjectAnalzyer : RepoProjectAnalyzer
    {
        public override Task Analyze(RepoProject project)
        {
            return Task.CompletedTask;
        }
    }

    public class RepoProjectAnalyzer
    {
        public static readonly RepoProjectAnalyzer Default = new RepoProjectAnalyzer();

        public static readonly RepoProjectAnalyzer Null = new NullRepoProjectAnalzyer();

        public virtual void CreateProjects(Repo repo) { }

        public virtual async Task Analyze(RepoProject project)
        {
            var analysisServices = project.Repo.AnalysisServices;
            analysisServices.Logger.WriteLine($"Analyzing project {project.ProjectId}");
            List<Task> fileTasks = new List<Task>();

            foreach (var file in project.Files)
            {
                if (file.PrimaryProject == project)
                {
                    if (analysisServices.ParallelProcessProjectFiles)
                    {
                        fileTasks.Add(analysisServices.TaskDispatcher.Invoke(() => file.Analyze(), TaskType.File));
                    }
                    else
                    {
                        await file.Analyze();
                    }
                }
            }

            await Task.WhenAll(fileTasks);

            await UploadProject(project);
        }

        public virtual void CreateProjects(RepoFile repoFile) { }

        public virtual bool IsCandidateProjectFile(RepoFile repoFile) => false;

        protected static async Task UploadProject(RepoProject project)
        {
            await project.ProjectContext.Finish(project);

            var analyzedProject = project.ProjectContext.Project;

            analyzedProject.ProjectKind = project.ProjectKind;
            foreach (var file in project.Files)
            {
                analyzedProject.Files.Add(new ProjectFileLink()
                {
                    RepoRelativePath = file.RepoRelativePath,
                    ProjectRelativePath = file.LogicalPath
                });
            }

            await project.Repo.AnalysisServices.RepositoryStore.AddProjectsAsync(new[] { analyzedProject });
        }
    }
}
