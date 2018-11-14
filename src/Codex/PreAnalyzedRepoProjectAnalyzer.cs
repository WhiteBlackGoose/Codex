using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Codex.Analysis.Files;
using Codex.Import;
using Codex.ObjectModel;

namespace Codex.Analysis
{
    public class PreAnalyzedRepoProjectAnalyzer : RepoProjectAnalyzer
    {
        private ConcurrentDictionary<string, AnalyzedProject> projectsById = new ConcurrentDictionary<string, AnalyzedProject>();

        public override void CreateProjects(Repo repo)
        {
            foreach (var project in projectsById.Values)
            {
                if (project.ProjectId == repo.DefaultRepoProject.ProjectId)
                {
                    continue;
                }

                var projectFile = GetProjectFile(repo, project);

                var repoProject = repo.CreateRepoProject(
                    project.ProjectId,
                    GetProjectDirectory(repo, project),
                    projectFile);

                repoProject.Analyzer = this;

                foreach (var file in project.Files)
                {
                    var repoFile = repoProject.AddFile(GetRepoPath(repo, file.RepoRelativePath), file.ProjectRelativePath);

                    repoFile.MarkAnalyzed();
                }

                // Clear files list since it will be recomputed
                project.Files.Clear();
            }
        }

        private string GetRepoPath(Repo repo, string repoRelativePath)
        {
            return Path.Combine(repo.DefaultRepoProject.ProjectDirectory, repoRelativePath);
        }

        private RepoFile GetProjectFile(Repo repo, AnalyzedProject project)
        {
            if (project.PrimaryFile == null)
            {
                return null;
            }

            return repo.DefaultRepoProject.AddFile(GetRepoPath(repo, project.PrimaryFile.RepoRelativePath));
        }

        private string GetProjectDirectory(Repo repo, AnalyzedProject project)
        {
            if (project.PrimaryFile == null)
            {
                // TODO: Is this ok?
                return @"\\Projects\";
            }

            return Path.Combine(repo.DefaultRepoProject.ProjectDirectory, Path.GetDirectoryName(project.PrimaryFile.RepoRelativePath));
        }

        public ICodexRepositoryStore CreateRepositoryStore(ICodexRepositoryStore innerStore)
        {
            return new RepositoryStore(this, innerStore);
        }

        private class RepositoryStore : ICodexRepositoryStore
        {
            private readonly PreAnalyzedRepoProjectAnalyzer analyzer;
            private readonly ICodexRepositoryStore innerStore;

            public RepositoryStore(PreAnalyzedRepoProjectAnalyzer analyzer, ICodexRepositoryStore innerStore)
            {
                this.analyzer = analyzer;
                this.innerStore = innerStore;
            }

            public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
            {
                return innerStore.AddBoundFilesAsync(files);
            }

            public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
            {
                return innerStore.AddCommitFilesAsync(links);
            }

            public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
            {
                return innerStore.AddLanguagesAsync(languages);
            }

            public Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
            {
                foreach (var project in projects)
                {
                    analyzer.projectsById[project.ProjectId] = project;
                }

                return innerStore.AddProjectsAsync(projects);
            }

            public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
            {
                return innerStore.AddTextFilesAsync(files);
            }

            public Task FinalizeAsync()
            {
                // TODO: Should we allow finalize?
                //return innerStore.FinalizeAsync();
                return Task.CompletedTask;
            }
        }
    }
}