using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Utilities;
using WebUI.Rendering;

namespace WebUI.Controllers
{
    public class SourceController : Controller
    {
        private readonly ICodex Storage;

        private readonly static IEqualityComparer<SymbolReferenceEntry> m_referenceEquator = new EqualityComparerBuilder<SymbolReferenceEntry>()
            .CompareByAfter(rs => rs.Span.Reference.Id)
            .CompareByAfter(rs => rs.Span.Reference.ProjectId)
            .CompareByAfter(rs => rs.Span.Reference.ReferenceKind)
            .CompareByAfter(rs => rs.ReferringProjectId)
            .CompareByAfter(rs => rs.ReferringFilePath);

        public SourceController(ICodex storage)
        {
            Storage = storage;
        }

        [Route("repos/{repoName}/source/{projectId}")]
        [Route("source/{projectId}")]
        public async Task<ActionResult> Index(string projectId, string filename, bool partial = false)
        {
            try
            {
                Requests.LogRequest(this);

                if (string.IsNullOrEmpty(filename))
                {
                    return this.HttpNotFound();
                }

                var getSourceResponse = await Storage.GetSourceAsync(new GetSourceArguments()
                {
                    ProjectId = projectId,
                    ProjectRelativePath = filename,
                    RepositoryScopeId = this.GetSearchRepo()
                });

                getSourceResponse.ThrowOnError();

                var boundSourceFile = getSourceResponse.Result;

                if (boundSourceFile == null)
                {
                    return PartialView("~/Views/Source/Index.cshtml", new EditorModel { Error = $"Bound source file for {filename} in {projectId} not found." });
                }

                var renderer = new SourceFileRenderer(boundSourceFile, projectId);

                Responses.PrepareResponse(Response);

                var model = await renderer.RenderAsync();
                model.IndexedOn = boundSourceFile.Commit?.DateUploaded.ToLocalTime().ToString() ?? "Unknown";
                model.RepoName = boundSourceFile.RepositoryName ?? "Unknown";
                model.IndexName = boundSourceFile.Commit?.CommitId;

                if (partial)
                {
                    return PartialView("~/Views/Source/Index.cshtml", (object)model);
                }
                else
                {
                    return View((object)model);
                }
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        [Route("repos/{repoName}/definitions/{projectId}")]
        [Route("definitions/{projectId}")]
        public async Task<ActionResult> GoToDefinitionAsync(string projectId, string symbolId)
        {
            try
            {
                Requests.LogRequest(this);

                var definitionResponse = await Storage.FindDefinitionLocationAsync(
                    new FindDefinitionLocationArguments()
                    {
                        RepositoryScopeId = this.GetSearchRepo(),
                        SymbolId = symbolId,
                        ProjectId = projectId,
                    });

                definitionResponse.ThrowOnError();

                var definitions = definitionResponse.Result;

                var definitions = await Storage.GetReferencesToSymbolAsync(
                    this.GetSearchRepos(),
                    new Symbol()
                    {
                        ProjectId = projectId,
                        Id = SymbolId.UnsafeCreateWithValue(symbolId),
                        Kind = nameof(ReferenceKind.Definition)
                    });

                definitions.Entries = definitions.Entries.Distinct(m_referenceEquator).ToList();

                if (definitions.Entries.Count == 1)
                {
                    var definitionReference = definitions.Entries[0];
                    return await Index(definitionReference.ReferringProjectId, definitionReference.File, partial: true);
                }
                else
                {
                    var definitionResult = await Storage.GetDefinitionsAsync(this.GetSearchRepos(), projectId, symbolId);
                    var symbolName = definitionResult?.FirstOrDefault()?.Span.Definition.DisplayName ?? symbolId;
                    definitions.SymbolName = symbolName ?? definitions.SymbolName;

                    if (definitions.Entries.Count == 0)
                    {
                        definitions = await Storage.GetReferencesToSymbolAsync(
                            this.GetSearchRepos(),
                            new Symbol()
                            {
                                ProjectId = projectId,
                                Id = SymbolId.UnsafeCreateWithValue(symbolId)
                            });
                    }

                    var referencesText = ReferencesController.GenerateReferencesHtml(definitions);
                    if (string.IsNullOrEmpty(referencesText))
                    {
                        referencesText = "No definitions found.";
                    }
                    else
                    {
                        referencesText = "<!--Definitions-->" + referencesText;
                    }

                    Responses.PrepareResponse(Response);

                    return PartialView("~/Views/References/References.cshtml", referencesText);
                }
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}