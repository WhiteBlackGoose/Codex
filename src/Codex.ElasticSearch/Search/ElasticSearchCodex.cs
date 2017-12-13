using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.ElasticProviders;
using Codex.Storage.Utilities;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static Codex.ElasticSearch.Utilities.ElasticUtility;

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

                var client = context.Client;

                var terms = searchPhrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var definitionsResult = await client.SearchAsync<IDefinitionSearchModel>(s => s
                        .Query(qcd => qcd.Bool(bq => bq
                            .Filter(GetTermsFilters(terms))
                            .Should(GetTermsFilters(terms, boostOnly: true))))
                        .Index(Configuration.Prefix + SearchTypes.Definition.IndexName)
                        .Take(arguments.MaxResults)
                        .CaptureRequest(context))
                    .ThrowOnFailure();

                if (definitionsResult.Hits.Count != 0)
                {
                    return new IndexQueryHits<ISearchResult>()
                    {
                        Hits = new List<ISearchResult>(definitionsResult.Hits.Select(hit =>
                            new SearchResult()
                            {
                                Definition = new DefinitionSymbol(hit.Source.Definition)
                            })),
                        Total = definitionsResult.Total
                    };
                }

                var textResults = await client.SearchAsync<ITextSourceSearchModel>(
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
                        .Index(Configuration.Prefix + SearchTypes.TextSource.IndexName)
                        .Take(arguments.MaxResults)
                        .CaptureRequest(context))
                    .ThrowOnFailure();

                var sourceFileResults =
                    (from hit in textResults.Hits
                     from highlightHit in hit.Highlights.Values
                     from highlight in highlightHit.Highlights
                     from span in FullTextUtilities.ParseHighlightSpans(highlight)
                     select new SearchResult()
                     {
                         TextLine = new TextLineSpanResult(hit.Source.File.Info)
                         {
                             TextSpan = span
                         }
                     }).ToList<ISearchResult>();

                return new IndexQueryHits<ISearchResult>()
                {
                    Hits = sourceFileResults,
                    Total = textResults.Total
                };
            });
        }

        private static Func<QueryContainerDescriptor<IDefinitionSearchModel>, QueryContainer> GetTermsFilter(string[] terms, bool boostOnly = false)
        {
            return qcd => qcd.Bool(bq => bq.Filter(GetTermsFilters(terms, boostOnly)));
        }

        private static IEnumerable<Func<QueryContainerDescriptor<IDefinitionSearchModel>, QueryContainer>>
            GetTermsFilters(string[] terms, bool boostOnly = false)
        {
            bool allowReferencedDefinitions = false;
            foreach (var term in terms)
            {
                if (term == "@all")
                {
                    allowReferencedDefinitions = true;
                }
                else
                {
                    yield return fq => ApplyTermFilter(term, fq, boostOnly);
                }
            }

            if (!boostOnly)
            {
                yield return fq => fq.Bool(bqd => bqd.MustNot(fq1 => fq1.Term(dss => dss.Definition.ExcludeFromDefaultSearch, true)));

                if (!allowReferencedDefinitions)
                {
                    // TODO: Should referenced symbols only be allowed conditionally
                    // Maybe it should be an option to the search arguments
                    //yield return fq => fq.Bool(bqd => bqd.MustNot(fq1 => fq1.Term(dss => dss.IsReferencedSymbol, true)));
                }
            }
        }

        private static QueryContainer FileFilter(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq)
        {
            if (term.Contains('.'))
            {
                return fq.Term(dss => dss.Keywords, term.ToLowerInvariant());
            }

            return fq;
        }

        private static QueryContainer NameFilter(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq, bool boostOnly)
        {
            var terms = term.CreateNameTerm();

            if (boostOnly)
            {
                return fq.Term(dss => dss.Definition.ShortName, terms.ExactNameTerm.ToLowerInvariant());
            }
            else
            {
                return fq.Term(dss => dss.Definition.ShortName, terms.NameTerm.ToLowerInvariant())
                        || fq.Term(dss => dss.Definition.ShortName, terms.SecondaryNameTerm.ToLowerInvariant());
            }
        }

        private static QueryContainer QualifiedNameTermFilters(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq)
        {
            var terms = ParseContainerAndName(term);

            // TEMPORARY HACK: This is needed due to the max length placed on container terms
            // The analyzer should be changed to use path_hierarchy with reverse option
            if ((terms.ContainerTerm.Length > (CustomAnalyzers.MaxGram - 2)) && terms.ContainerTerm.Contains("."))
            {
                terms.ContainerTerm = terms.ContainerTerm.SubstringAfterFirstOccurrence('.');
            }

            return fq.Bool(bq => bq.Filter(
                fq1 => fq1.Term(dss => dss.Definition.ShortName, terms.NameTerm.ToLowerInvariant())
                    || fq1.Term(dss => dss.Definition.ShortName, terms.SecondaryNameTerm.ToLowerInvariant()),
                fq1 => fq1.Term(dss => dss.Definition.ContainerQualifiedName, terms.ContainerTerm.ToLowerInvariant())));
        }

        private static QueryContainer ProjectTermFilters(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq)
        {
            var result = fq.Term(dss => dss.Definition.ProjectId, term)
                || fq.Term(dss => dss.Definition.ProjectId, term.Capitalize());
            if (term != term.ToLowerInvariant())
            {
                result |= fq.Term(dss => dss.Definition.ProjectId, term.ToLowerInvariant());
            }

            return result;
        }

        private static QueryContainer KindTermFilters(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq)
        {
            return fq.Term(dss => dss.Definition.Kind, term.ToLowerInvariant())
                || fq.Term(dss => dss.Definition.Kind, term.Capitalize());
        }

        private static QueryContainer IndexTermFilters(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq)
        {
            return fq.Term("_index", term.ToLowerInvariant());
        }

        private static QueryContainer ApplyTermFilter(string term, QueryContainerDescriptor<IDefinitionSearchModel> fq, bool boostOnly)
        {
            var d = NameFilter(term, fq, boostOnly);

            if (!boostOnly)
            {
                d |= FileFilter(term, fq);
                d |= QualifiedNameTermFilters(term, fq);
                d |= ProjectTermFilters(term, fq);
                d |= IndexTermFilters(term, fq);
                d |= KindTermFilters(term, fq);
            }

            return d;
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
