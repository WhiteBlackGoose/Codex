using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage;
using WebUI.Models;

namespace WebUI.Controllers
{
    public class SearchController : Controller
    {
        private readonly ICodex codex;

        public SearchController(ICodex codex)
        {
            this.codex = codex;
        }

        // GET: Results
        [System.Web.Mvc.Route("repos/{repoName}/search/ResultsAsHtml")]
        [System.Web.Mvc.Route("search/ResultsAsHtml")]
        public async Task<ActionResult> ResultsAsHtml([FromUri(Name = "q")] string searchTerm)
        {
            try
            {
                Requests.LogRequest(this, searchTerm);
                searchTerm = HttpUtility.UrlDecode(searchTerm);

                if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 3)
                {
                    //Still render view even if we have an invalid search term - it'll display a "results not found" message
                    Debug.WriteLine("GetSearchResult - searchTerm is null or whitespace");
                    return PartialView();
                }

                //string term;
                //Classification? classification;
                //ParseSearchTerm(searchTerm, out term, out classification);

                Responses.PrepareResponse(Response);

                var searchResult = await codex.SearchAsync(new SearchArguments()
                {
                    RepositoryScopeId = this.GetSearchRepo(),
                    SearchString = searchTerm,
                });

                searchResult.ThrowOnError();

                if (searchResult.Result.Total == 0 || searchResult.Result.Hits[0].Definition != null)
                {
                    return PartialSymbolSearchResultView(searchTerm, searchResult);
                }
                else
                {
                    return PartialTextSearchResultView(searchTerm, searchResult);
                }
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private SymbolSearchResult ToSymbolSearchResult(string searchTerm, IndexQueryHitsResponse<ISearchResult> searchResponse)
        {
            return new SymbolSearchResult()
            {
                Total = (int)searchResponse.Result.Total,
                QueryText = searchTerm,
                Entries = new List<SymbolSearchResultEntry>(searchResponse.Result.Hits.Select(result => result.Definition)
                    .Select(symbol =>
                    {
                        return new SymbolSearchResultEntry()
                        {
                            File = symbol.Kind == nameof(SymbolKinds.File) && symbol.ShortName != null ?
                                Path.Combine(symbol.ContainerQualifiedName, symbol.ShortName) : null,
                            Symbol = symbol,
                            Glyph = symbol.GetGlyph() + ".png"
                        };
                    }))
            };
        }

        private PartialViewResult PartialSymbolSearchResultView(string searchTerm, IndexQueryHitsResponse<ISearchResult> searchResponse)
        {
            return PartialView(ToSymbolSearchResult(searchTerm, searchResponse));
        }

        private ReferencesResult ToReferencesResult(string searchTerm, IndexQueryHitsResponse<ISearchResult> searchResponse)
        {
            return new ReferencesResult()
            {
                SymbolDisplayName = searchTerm,
                Total = searchResponse.Result.Total,
                Hits = new List<IReferenceSearchResult>(searchResponse.Result.Hits.Select(result => result.TextLine)
                    .Select(textResult => new ReferenceSearchResult(textResult)
                    {
                        ReferenceSpan = new ReferenceSpan(textResult.TextSpan)
                        {
                            Reference = new ReferenceSymbol()
                            {
                                ReferenceKind = nameof(ReferenceKind.Text),
                            }
                        }
                    }))
            };
        }

        private PartialViewResult PartialTextSearchResultView(string searchTerm, IndexQueryHitsResponse<ISearchResult> searchResponse)
        {
            return PartialView(
                "~/Views/References/References.cshtml", 
                (object)ReferencesController.GenerateReferencesHtml(ToReferencesResult(searchTerm, searchResponse)));
        }

        private static void ParseSearchTerm(string searchTerm, out string term, out Classification? classification)
        {
            term = searchTerm;
            classification = null;

            var pieces = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 2)
            {
                Classification parsedClassification;
                if (Enum.TryParse(pieces[0], true, out parsedClassification))
                {
                    classification = parsedClassification;
                    term = pieces[1];
                }
            }
        }
    }
}