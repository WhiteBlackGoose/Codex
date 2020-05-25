using System;
using System.Threading;
using System.Threading.Tasks;
using Codex.Import;
using Codex.ObjectModel;

namespace Codex.Analysis.Files
{
    public class RepoFileAnalyzer
    {
        public static readonly RepoFileAnalyzer Default = new RepoFileAnalyzer();

        public static readonly RepoFileAnalyzer Null = new NullRepoFileAnalyzer();

        public virtual string[] SupportedExtensions => new string[0];

        public Task Analyze(RepoFile file)
        {
            return Analyze(file.PrimaryProject.Repo.AnalysisServices, file);
        }

        public virtual void Initialize(Repo repo)
        {
        }

        public virtual void Finalize(Repo repo)
        {
        }

        public virtual SourceFileInfo AugmentSourceFileInfo(SourceFileInfo info)
        {
            return info;
        }

        protected virtual async Task Analyze(AnalysisServices services, RepoFile file)
        {
            try
            {
                ReportStartAnalyze(file);

                SourceFile sourceFile = file.InMemorySourceFileBuilder?.SourceFile ?? CreateSourceFile(services, file);

                BoundSourceFileBuilder binder = file.InMemorySourceFileBuilder ?? CreateBuilder(sourceFile, file, file.PrimaryProject.ProjectId);

                await AnnotateAndUpload(services, file, binder);
            }
            catch (Exception ex)
            {
                services.Logger.LogExceptionError($"Analyzing file: {file.FilePath}", ex);
            }
        }

        private SourceFile CreateSourceFile(AnalysisServices services, RepoFile file)
        {
            return new SourceFile()
            {
                Info = AugmentSourceFileInfo(new SourceFileInfo()
                {
                    Language = "text",
                    ProjectRelativePath = file.LogicalPath,
                    RepoRelativePath = file.RepoRelativePath
                }),
                Content = file.InMemoryContent ?? services.ReadAllText(file.FilePath)
            };
        }

        private async Task AnnotateAndUpload(
            AnalysisServices services,
            RepoFile file,
            BoundSourceFileBuilder binder)
        {
            await AnnotateFileAsync(services, file, binder);

            var boundSourceFile = binder.Build();

            await UploadSourceFile(services, file, boundSourceFile);
        }

        protected static void ReportStartAnalyze(RepoFile file)
        {
            int analyzeCount = Interlocked.Increment(ref file.PrimaryProject.Repo.AnalyzeCount);
            file.PrimaryProject.Repo.AnalysisServices.Logger.WriteLine($"Analyzing source: '{file.PrimaryProject.ProjectId}::{file.LogicalPath}' ({analyzeCount} of {file.PrimaryProject.Repo.FileCount})");
        }

        protected BoundSourceFileBuilder CreateBuilder(SourceFile sourceFile, RepoFile repoFile, string projectId)
        {
            var builder = CreateBuilderCore(sourceFile, projectId);
            if (sourceFile.Info.RepoRelativePath != null && repoFile.PrimaryProject != repoFile.PrimaryProject.Repo.DefaultRepoProject)
            {
                // Add a file definition for the repo relative path in the default repo project
                builder.AnnotateReferences(0, 0, BoundSourceFileBuilder.CreateFileReferenceSymbol(
                    sourceFile.Info.RepoRelativePath,
                    repoFile.PrimaryProject.Repo.DefaultRepoProject.ProjectId,
                    isDefinition: true));
            }

            return builder;
        }

        protected virtual BoundSourceFileBuilder CreateBuilderCore(SourceFile sourceFile, string projectId)
        {
            return new BoundSourceFileBuilder(sourceFile, projectId);
        }

        protected static Task UploadSourceFile(AnalysisServices services, RepoFile file, BoundSourceFile boundSourceFile)
        {
            boundSourceFile.RepositoryName = file.PrimaryProject.Repo.Name;
            boundSourceFile.SourceFile.Info.RepositoryName = file.PrimaryProject.Repo.Name;

            int uploadCount = Interlocked.Increment(ref file.PrimaryProject.Repo.UploadCount);
            file.PrimaryProject.Repo.AnalysisServices.Logger.WriteLine($"Uploading source: '{boundSourceFile.ProjectId}::{boundSourceFile.SourceFile.Info.ProjectRelativePath}' ({uploadCount} of {file.PrimaryProject.Repo.FileCount})");

            return services.RepositoryStore.AddBoundFilesAsync(new[] { boundSourceFile });
        }

        protected virtual Task AnnotateFileAsync(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            AnnotateFile(services, file, binder);
            return Task.FromResult(true);
        }

        protected virtual void AnnotateFile(AnalysisServices services, RepoFile file, BoundSourceFileBuilder binder)
        {
            AnnotateFile(binder);
        }

        protected virtual void AnnotateFile(BoundSourceFileBuilder binder)
        {
            var text = binder.SourceFile.Content;
            if (XmlAnalyzer.IsXml(text))
            {
                XmlAnalyzer.Analyze(binder);
            }
        }

        public class NullRepoFileAnalyzer : RepoFileAnalyzer
        {
            protected override Task Analyze(AnalysisServices services, RepoFile file)
            {
                return Task.CompletedTask;
            }
        }
    }
}
