using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codex.Analysis.Files;
using Codex.Import;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Projects
{
    public class ManagedProjectAnalyzer : RepoProjectAnalyzerBase
    {
        public readonly SemanticServices semanticServices;
        private readonly Lazy<Task<Solution>> lazySolution;
        private Project Project;
        private CompilationServices CompilationServices;
        private Compilation Compilation;
        private AnalyzedProjectContext ProjectContext;
        private readonly ProjectId ProjectId;
        private CompletionTracker CompletionTracker = new CompletionTracker();

        public ManagedProjectAnalyzer(
            SemanticServices semanticServices,
            RepoProject repoProject,
            ProjectId projectId,
            Lazy<Task<Solution>> lazySolution)
        {
            this.semanticServices = semanticServices;
            ProjectId = projectId;
            this.lazySolution = lazySolution;
            ProjectContext = repoProject.ProjectContext;
        }

        public override async Task Analyze(RepoProject project)
        {
            var logger = project.Repo.AnalysisServices.Logger;
            try
            {
                var services = project.Repo.AnalysisServices;
                logger.LogMessage("Loading project: " + project.ProjectId);

                var solution = await lazySolution.Value;
                Project = solution.GetProject(ProjectId);
                if (Project == null)
                {
                    logger.LogError($"Can't find project for {ProjectId} in {solution}");
                }
                else
                {
                    Project = Project.WithMetadataReferences(Project.MetadataReferences.Where(m => !(m is UnresolvedMetadataReference)));

                    Compilation = await Project.GetCompilationAsync();
                    CompilationServices = new CompilationServices(Compilation);

                    foreach (var reference in Compilation.ReferencedAssemblyNames)
                    {
                        var referencedProject = new ReferencedProject()
                        {
                            ProjectId = reference.Name,
                            DisplayName = reference.GetDisplayName(),
                            Properties = new PropertyMap()
                                {
                                    { "PublicKey", string.Concat(reference.PublicKey.Select(b => b.ToString("X2"))) }
                                }
                        };

                        ProjectContext.ReferencedProjects.TryAdd(referencedProject.ProjectId, referencedProject);
                    }
                }

                await base.Analyze(project);

                project.Analyzer = RepoProjectAnalyzer.Null;
            }
            catch (Exception ex)
            {
                logger.LogExceptionError($"Loading project {project.ProjectId}", ex);
            }
        }

        public RepoFileAnalyzer CreateFileAnalyzer(DocumentInfo document)
        {
            return new FileAnalyzer(this, document);
        }

        private class FileAnalyzer : RepoFileAnalyzer
        {
            private ManagedProjectAnalyzer ProjectAnalyzer;
            private readonly DocumentInfo DocumentInfo;

            public FileAnalyzer(ManagedProjectAnalyzer projectAnalyzer, DocumentInfo documentInfo)
            {
                if (projectAnalyzer == null)
                {
                    throw new ArgumentNullException(nameof(projectAnalyzer));
                }

                if (documentInfo == null)
                {
                    throw new ArgumentNullException(nameof(documentInfo));
                }

                ProjectAnalyzer = projectAnalyzer;
                DocumentInfo = documentInfo;
            }

            protected override async Task Analyze(AnalysisServices services, RepoFile file)
            {
                try
                {
                    ReportStartAnalyze(file);

                    var project = ProjectAnalyzer.Project;
                    if (project == null)
                    {
                        file.PrimaryProject.Repo.AnalysisServices.Logger.LogError("Project is null");
                        return;
                    }

                    var document = project.GetDocument(DocumentInfo.Id);
                    var text = await document.GetTextAsync();

                    SourceFile sourceFile = new SourceFile()
                    {
                        Info = AugmentSourceFileInfo(new SourceFileInfo()
                        {
                            Language = project.Language,
                            ProjectRelativePath = file.LogicalPath,
                            RepoRelativePath = file.RepoRelativePath
                        }),
                    };

                    BoundSourceFileBuilder binder = CreateBuilder(sourceFile, file, file.PrimaryProject.ProjectId);
                    binder.SourceText = text;

                    DocumentAnalyzer analyzer = new DocumentAnalyzer(
                        ProjectAnalyzer.semanticServices,
                        document,
                        ProjectAnalyzer.CompilationServices,
                        file.LogicalPath,
                        ProjectAnalyzer.ProjectContext,
                        binder);

                    var boundSourceFile = await analyzer.CreateBoundSourceFile();

                    ProjectAnalyzer.ProjectContext.ReportDocument(boundSourceFile, file);

                    await UploadSourceFile(services, file, boundSourceFile);
                }
                finally
                {
                    file.Analyzer = RepoFileAnalyzer.Null;
                    ProjectAnalyzer = null;
                }
            }
        }
    }
}
