using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.ElasticProviders;
using Codex.Storage.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Search
{
    public class ElasticSearchCodex : ICodex
    {
        internal readonly ElasticSearchService Service;
        internal readonly ElasticSearchStoreConfiguration Configuration;

        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchCodex(ElasticSearchStoreConfiguration configuration, ElasticSearchService service)
        {
            Configuration = configuration;
            Service = service;
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<IBoundSourceSearchModel>> GetSourceAsync(GetSourceArguments arguments)
        {
            throw new NotImplementedException();
        }

        public async Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            var searchPhrase = arguments.SearchString;

            searchPhrase = searchPhrase?.Trim();
            bool isPrefix = searchPhrase?.EndsWith("*") ?? false;
            searchPhrase = searchPhrase?.TrimEnd('*');

            if (string.IsNullOrEmpty(searchPhrase) || searchPhrase.Length < 3)
            {
                return new IndexQueryHitsResponse<ISearchResult>()
                {
                    Error = "Search phrase must be at least 3 characters"
                };
            }

            return await UseClient(async context =>
            {
                Placeholder.Todo("Do definitions search");
                Placeholder.Todo("Allow filtering text matches by extension/path");
                Placeholder.Todo("Extract method for getting index name from search type");

                var indices = (Configuration.Prefix + SearchTypes.TextSource.IndexName).ToLowerInvariant();

                var client = context.Client;

                var result = await client.SearchAsync<ITextSourceSearchModel>(
                    s => s
                        .Query(f =>
                            f.Bool(bq =>
                            bq.Filter(qcd => !qcd.Term(sf => sf.File.ExcludeFromSearch, true))
                              .Must(qcd => qcd.ConfigureIfElse(isPrefix,
                                f0 => f0.MatchPhrasePrefix(mpp => mpp.Field(sf => sf.File.Content).Query(searchPhrase).MaxExpansions(100)),
                                f0 => f0.MatchPhrase(mpp => mpp.Field(sf => sf.File.Content).Query(searchPhrase))))))
                        .Highlight(h => h.Fields(hf => hf.Field(sf => sf.File.Content).BoundaryCharacters("\n\r")))
                        .Source(source => source
                            .Includes(sd => sd.Fields(
                                sf => sf.File.Info)))
                        .Index(indices)
                        .Take(arguments.MaxResults).CaptureRequest(context))
                    .ThrowOnFailure();

                var sourceFileResults =
                    (from hit in result.Hits
                     from highlightHit in hit.Highlights.Values
                     from highlight in highlightHit.Highlights
                     from span in FullTextUtilities.ParseHighlightSpans(highlight)
                     select new SearchResult(hit.Source.File.Info)
                     {
                         TextSpan = span
                     }).ToList<ISearchResult>();

                return new IndexQueryHits<ISearchResult>()
                {
                    Hits = sourceFileResults,
                    Total = (int)result.Total
                };
            });
        }

        private async Task<IndexQueryHitsResponse<T>> UseClient<T>(Func<ClientContext, Task<IndexQueryHits<T>>> useClient)
        {
            var elasticResponse = await Service.UseClient(async context =>
            {
                try
                {
                    var result = await useClient(context);
                    return new IndexQueryHitsResponse<T>()
                    {
                        Result = result
                    };
                }
                catch (Exception ex)
                {
                    return new IndexQueryHitsResponse<T>()
                    {
                        Error = ex.ToString(),
                    };
                }
            });

            var response = elasticResponse.Result;
            response.RawQueries = elasticResponse.Requests.ToList();
            response.Duration = elasticResponse.Duration;
            return response;
        }
    }
}
