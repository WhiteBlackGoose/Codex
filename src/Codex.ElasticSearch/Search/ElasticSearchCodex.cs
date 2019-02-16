using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.ElasticProviders;
using Codex.Storage.Utilities;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

using static Codex.ElasticSearch.Utilities.ElasticUtility;
using static Codex.ElasticSearch.StoredFilterUtilities;

namespace Codex.ElasticSearch.Search
{
    public class ElasticSearchCodex : ICodex
    {
        internal readonly ElasticSearchService Service;
        internal readonly ElasticSearchStoreConfiguration Configuration;

        private ConcurrentDictionary<string, (DateTime resolveTime, string repositorySnapshotId)> resolvedRepositoryIds = new ConcurrentDictionary<string, (DateTime resolveTime, string repositorySnapshotId)>();

        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchCodex(ElasticSearchStoreConfiguration configuration, ElasticSearchService service)
        {
            Configuration = configuration;
            Service = service;
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return FindReferencesCore(arguments, async context =>
            {
                var client = context.Client;
                var referencesResult = await client.SearchAsync<IReferenceSearchModel>(s => s
                    .StoredFilterSearch(context, IndexName(SearchTypes.Reference), qcd => qcd.Bool(bq => bq.Filter(
                        fq => fq.Term(r => r.Reference.ProjectId, arguments.ProjectId),
                        fq => fq.Term(r => r.Reference.Id, arguments.SymbolId))))
                    .Sort(sd => sd.Ascending(r => r.ProjectId))
                    .Take(arguments.MaxResults))
                .ThrowOnFailure();

                return referencesResult;
            });
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            return FindReferencesCore(arguments, async context =>
            {
                var client = context.Client;
                var referencesResult = await client.SearchAsync<IReferenceSearchModel>(s => s
                        .StoredFilterSearch(context, IndexName(SearchTypes.Reference), qcd => qcd.Bool(bq => bq.Filter(
                            fq => fq.Term(r => r.Reference.ProjectId, arguments.ProjectId),
                            fq => fq.Term(r => r.Reference.Id, arguments.SymbolId),
                            fq => fq.Term(r => r.Reference.ReferenceKind, nameof(ReferenceKind.Definition)))))
                        .Take(arguments.MaxResults))
                    .ThrowOnFailure();

                if (arguments.FallbackFindAllReferences && referencesResult.Total == 0)
                {
                    // No definitions, return the the result of find all references 
                    referencesResult = await client.SearchAsync<IReferenceSearchModel>(s => s
                        .StoredFilterSearch(context, IndexName(SearchTypes.Reference), qcd => qcd.Bool(bq => bq.Filter(
                            fq => fq.Term(r => r.Reference.ProjectId, arguments.ProjectId),
                            fq => fq.Term(r => r.Reference.Id, arguments.SymbolId))))
                        .Sort(sd => sd.Ascending(r => r.ProjectId))
                        .Take(arguments.MaxResults))
                    .ThrowOnFailure();
                }

                return referencesResult;
            });
        }

        private async Task<IndexQueryResponse<ReferencesResult>> FindReferencesCore(FindSymbolArgumentsBase arguments, Func<StoredFilterSearchContext, Task<ISearchResponse<IReferenceSearchModel>>> getReferencesAsync)
        {
            return await UseClientSingle(arguments, async context =>
            {
                var client = context.Client;

                ISearchResponse<IReferenceSearchModel> referencesResult = await getReferencesAsync(context);

                var displayName = await GetSymbolShortName(context, arguments);

                var searchResults =
                    (from hit in referencesResult.Hits
                     let referenceSearchModel = hit.Source
                     from span in referenceSearchModel.Spans
                     select new ReferenceSearchResult(referenceSearchModel)
                     {
                         ReferenceSpan = new ReferenceSpan(span)
                         {
                             Reference = new ReferenceSymbol(referenceSearchModel.Reference)
                         }
                     }).ToList<IReferenceSearchResult>();


                return new ReferencesResult()
                {
                    SymbolDisplayName = displayName,
                    Hits = searchResults,
                    Total = referencesResult.Total
                };
            });
        }

        private async Task<string> GetSymbolShortName(StoredFilterSearchContext context, FindSymbolArgumentsBase arguments)
        {
            var client = context.Client;
            var definitionsResult = await client.SearchAsync<IDefinitionSearchModel>(s => s
                    .StoredFilterSearch(context, IndexName(SearchTypes.Definition), qcd => qcd.Bool(bq => bq.Filter(
                        fq => fq.Term(r => r.Definition.ProjectId, arguments.ProjectId),
                        fq => fq.Term(r => r.Definition.Id, arguments.SymbolId))))
                    .Take(1))
                .ThrowOnFailure();

            return definitionsResult.Hits.FirstOrDefault()?.Source.Definition.ShortName;
        }

        public async Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            // TODO: Add get repo for getting uploaded date and web address.
            return await UseClientSingle<IBoundSourceFile>(arguments, async context =>
            {
                var client = context.Client;

                var boundResults = await client.SearchAsync<BoundSourceSearchModel>(sd => sd
                    .StoredFilterSearch(context, IndexName(SearchTypes.BoundSource), qcd => qcd.Bool(bq => bq.Filter(
                            fq => fq.Term(s => s.File.Info.ProjectId, arguments.ProjectId),
                            fq => fq.Term(s => s.File.Info.ProjectRelativePath, arguments.ProjectRelativePath))))
                    .Take(1))
                .ThrowOnFailure();

                if (boundResults.Hits.Count != 0)
                {
                    var boundSearchModel = boundResults.Hits.First().Source;
                    //var textResults = await client.GetAsync<TextSourceSearchModel>(boundSearchModel.TextUid,
                    //    gd => gd.Index(Configuration.Prefix + SearchTypes.TextSource.IndexName)
                    //            .Routing(GetRouting(boundSearchModel.TextUid)))
                    //.ThrowOnFailure();

                    var chunks = await Service.GetAsync(SearchTypes.TextChunk, IndexName(SearchTypes.TextChunk), boundSearchModel.File.Chunks.SelectList(c => c.Id));

                    var repoResults = await client.SearchAsync<RepositorySearchModel>(sd => sd
                        .StoredFilterSearch(context, IndexName(SearchTypes.Repository), qcd => qcd.Bool(bq => bq.Filter(
                                fq => fq.Term(s => s.Repository.Name, boundSearchModel.File.Info.RepositoryName))))
                        .Take(1))
                    .ThrowOnFailure();

                    var commitResults = await client.SearchAsync<CommitSearchModel>(sd => sd
                        .StoredFilterSearch(context, IndexName(SearchTypes.Commit), qcd => qcd.Bool(bq => bq.Filter(
                                fq => fq.Term(s => s.Commit.RepositoryName, boundSearchModel.File.Info.RepositoryName))))
                        .Take(1))
                    .ThrowOnFailure();

                    var repo = repoResults.Hits.FirstOrDefault()?.Source.Repository;
                    var commit = commitResults.Hits.FirstOrDefault()?.Source.Commit;

                    var sourceFile = boundSearchModel.File;
                    if (sourceFile.Info.WebAddress == null
                        && repo?.SourceControlWebAddress != null
                        && sourceFile.Info.RepoRelativePath != null
                        // Don't add web access link for files not under source tree (i.e. [Metadata])
                        && !sourceFile.Info.RepoRelativePath.StartsWith("["))
                    {
                        sourceFile.Info.WebAddress = StoreUtilities.GetFileWebAddress(repo.SourceControlWebAddress, sourceFile.Info.RepoRelativePath);
                    }

                    return new BoundSourceFile(boundSearchModel.BindingInfo)
                    {
                        SourceFile = TextIndexingUtilities.FromChunks(boundSearchModel.File, chunks),
                        Commit = commit,
                        Repo = repo
                    };
                }

                throw new Exception("Unable to find source file");
            });
        }

        public async Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments)
        {
            return await UseClientSingle<GetProjectResult>(arguments, async context =>
            {
                var client = context.Client;

                var response = await client.SearchAsync<ProjectSearchModel>(sd => sd
                    .StoredFilterSearch(context, IndexName(SearchTypes.Project), qcd => qcd.Bool(bq => bq.Filter(
                            fq => fq.Term(s => s.Project.ProjectId, arguments.ProjectId))))
                    .Take(1))
                .ThrowOnFailure();

                if (response.Hits.Count != 0)
                {
                    IProjectSearchModel projectSearchModel = response.Hits.First().Source;

                    // TODO: Not sure why this is a good marker for when the project was uploaded. Since project search model may be deduplicated,
                    // to a past result we probably need something more accurate. Maybe the upload date of the stored filter. That would more closely
                    // match the legacy behavior.
                    var commitResponse = await client.SearchAsync<ICommitSearchModel>(sd => sd
                        .StoredFilterSearch(context, IndexName(SearchTypes.Commit), qcd => qcd.Bool(bq => bq.Filter(
                                fq => fq.Term(s => s.Commit.RepositoryName, projectSearchModel.Project.RepositoryName))))
                        .Take(1)
                        .CaptureRequest(context));

                    var referencesResult = await client.SearchAsync<IProjectReferenceSearchModel>(s => s
                        .StoredFilterSearch(context, IndexName(SearchTypes.ProjectReference), qcd => qcd.Bool(bq => bq.Filter(
                            fq => fq.Term(r => r.ProjectReference.ProjectId, arguments.ProjectId))))
                        .Sort(sd => sd.Ascending(r => r.ProjectId))
                        .Source(sfd => sfd.Includes(f => f.Field(r => r.ProjectId)))
                        .Take(arguments.MaxResults))
                    .ThrowOnFailure();

                    return new GetProjectResult()
                    {
                        Project = projectSearchModel.Project,
                        DateUploaded = commitResponse?.Hits.FirstOrDefault()?.Source.Commit.DateUploaded ?? default(DateTime),
                        ReferencingProjects = referencesResult?.Hits.Select(h => h.Source.ProjectId).ToList()
                    };
                }

                throw new Exception("Unable to find project information");
            });
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

            return await UseClient(arguments, async context =>
            {
                Placeholder.Todo("Allow filtering text matches by extension/path");
                Placeholder.Todo("Extract method for getting index name from search type");

                var client = context.Client;

                if (!arguments.TextSearch)
                {
                    var terms = searchPhrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool allowReferencedDefinitions = terms.Any(t => IsAllowReferenceDefinitionsTerm(t)) || arguments.AllowReferencedDefinitions;
                    terms = terms.Where(t => !IsAllowReferenceDefinitionsTerm(t)).ToArray();

                    var indexName = IndexName(SearchTypes.Definition);
                    var definitionsResult = await client.SearchAsync<IDefinitionSearchModel>(s => s
                            .StoredFilterSearch(context, indexName, qcd => qcd.Bool(bq => bq
                                .Filter(GetTermsFilter(terms, allowReferencedDefinitions: allowReferencedDefinitions))
                                .Should(GetTermsFilter(terms, boostOnly: true))),
                                filterIndexName: allowReferencedDefinitions ? indexName : GetDeclaredDefinitionsIndexName(indexName))
                            .Take(arguments.MaxResults))
                        .ThrowOnFailure();

                    if (definitionsResult.Hits.Count != 0 || !arguments.FallbackToTextSearch)
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
                }

                // Fallback to performing text phrase search
                var textChunkResults = await client.SearchAsync<ITextChunkSearchModel>(
                    s => s
                        .StoredFilterSearch(context, IndexName(SearchTypes.TextChunk), f =>
                            f.Bool(bq =>
                            bq.Must(qcd => qcd.ConfigureIfElse(isPrefix,
                                f0 => f0.MatchPhrasePrefix(mpp => mpp.Field(sf => sf.Chunk.ContentLines).Query(searchPhrase).MaxExpansions(100)),
                                f0 => f0.MatchPhrase(mpp => mpp.Field(sf => sf.Chunk.ContentLines).Query(searchPhrase))))))
                        .Highlight(h => h.Fields(hf => hf.Field(sf => sf.Chunk.ContentLines).BoundaryCharacters("\n\r")))
                        .Take(arguments.MaxResults))
                    .ThrowOnFailure();

                var chunkIds = textChunkResults.Hits.ToDictionarySafe(s => s.Id);

                var textResults = await client.SearchAsync<ITextSourceSearchModel>(
                    s => s
                        .StoredFilterSearch(context, IndexName(SearchTypes.TextSource), qcd =>
                            qcd.Terms(tq => tq.Terms(chunkIds.Keys).Field(tss => tss.File.Chunks.First().Id)))
                        .Take(arguments.MaxResults))
                    .ThrowOnFailure();

                var sourceFileResults =
                   (from hit in textResults.Hits
                    from chunkHit in hit.Source.File.Chunks.Select(c => (hit: chunkIds.GetOrDefault(c.Id), c.StartLineNumber)).Where(h => h.hit != null)
                    from highlightHit in chunkHit.hit.Highlights.Values
                    from highlight in highlightHit.Highlights
                    from span in FullTextUtilities.ParseHighlightSpans(highlight, lineOffset: chunkHit.StartLineNumber)
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

        private static bool IsAllowReferenceDefinitionsTerm(string t)
        {
            return t.Equals("@all", StringComparison.OrdinalIgnoreCase);
        }

        private string IndexName(SearchType searchType)
        {
            return Configuration.Prefix + searchType.IndexName;
        }

        private Func<QueryContainerDescriptor<IDefinitionSearchModel>, QueryContainer> GetTermsFilter(
            string[] terms,
            bool boostOnly = false,
            bool allowReferencedDefinitions = false)
        {
            return qcd => qcd.Bool(bq => bq.Filter(GetTermsFilters(terms, boostOnly, allowReferencedDefinitions)));
        }

        private IEnumerable<Func<QueryContainerDescriptor<IDefinitionSearchModel>, QueryContainer>>
            GetTermsFilters(
            string[] terms,
            bool boostOnly = false,
            bool allowReferencedDefinitions = false)
        {
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

                //if (!allowReferencedDefinitions)
                //{
                //    yield return fq => fq.Terms(
                //        tsd => tsd
                //            .Field(e => e.StableId)
                //            .TermsLookup<IStoredFilter>(ld => ld
                //                .Index(IndexName(SearchTypes.StoredFilter))
                //                .Id(GetFilterName(
                //                    Configuration.CombinedSourcesFilterName, 
                //                    indexName: GetDeclaredDefinitionsIndexName(IndexName(SearchTypes.Definition))))
                //                .Path(sf => sf.StableIds)));
                //    // TODO: Should referenced symbols only be allowed conditionally
                //    // Maybe it should be an option to the search arguments
                //    //yield return fq => fq.Bool(bqd => bqd.MustNot(fq1 => fq1.Term(dss => dss.IsReferencedSymbol, true)));
                //}
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
                // Advanced case where symbol id is mentioned as a term
                d |= fq.Term(dss => dss.Definition.Id, term.ToLowerInvariant());
                d |= FileFilter(term, fq);
                d |= QualifiedNameTermFilters(term, fq);
                d |= ProjectTermFilters(term, fq);
                d |= IndexTermFilters(term, fq);
                d |= KindTermFilters(term, fq);
            }

            return d;
        }

        private async Task<StoredFilterSearchContext> GetStoredFilterContextAsync(ContextCodexArgumentsBase arguments, ClientContext context)
        {
            string resolvedAliasStoredFilterPrefix = null;
            var repositoryId = arguments.RepositoryScopeId ?? Configuration.CombinedSourcesFilterName;
            var aliasUid = GetStoredFilterAliasUid(repositoryId);

            if (!resolvedRepositoryIds.TryGetValue(repositoryId, out var resolvedEntry) || !GetValueFromEntry(resolvedEntry, out resolvedAliasStoredFilterPrefix))
            {
                IGetResponse<PropertySearchModel> aliasResult = await context.Client.GetAsync<PropertySearchModel>(aliasUid,
                    gd => gd.Index(IndexName(SearchTypes.Property)))
                    .ThrowOnFailure();

                if (aliasResult.Found)
                {
                    resolvedAliasStoredFilterPrefix = aliasResult.Source.Value;
                    resolvedRepositoryIds.TryAdd(repositoryId, (DateTime.UtcNow, resolvedAliasStoredFilterPrefix));
                }
            }

            if (resolvedAliasStoredFilterPrefix == null)
            {
                throw new Exception($"Unable to find index with name: {repositoryId}");
            }

            return new StoredFilterSearchContext(
                context,
                repositoryScopeId: repositoryId,
                storedFilterIndexName: IndexName(SearchTypes.StoredFilter),
                // repos/{repositoryName}/{ingestId}
                storedFilterUidPrefix: resolvedAliasStoredFilterPrefix);
        }

        private bool GetValueFromEntry((DateTime resolveTime, string repositorySnapshotId) resolvedEntry, out string aliasId)
        {
            var age = DateTime.UtcNow - resolvedEntry.resolveTime;
            if (age > Configuration.CachedAliasIdRetention)
            {
                // Entry is too old, need resolve the alias id
                aliasId = null;
                return false;
            }

            aliasId = resolvedEntry.repositorySnapshotId;
            return true;
        }

        private async Task<IndexQueryHitsResponse<T>> UseClient<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext, Task<IndexQueryHits<T>>> useClient)
        {
            var elasticResponse = await Service.UseClient(async context =>
            {
                try
                {
                    var sfContext = await GetStoredFilterContextAsync(arguments, context);
                    var result = await useClient(sfContext);
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

        public Task<IndexQueryHitsResponse<IRegisteredEntity>> GetRegisteredEntitiesAsync(SearchType searchType)
        {
            var arguments = new ContextCodexArgumentsBase();
            return UseClient<IRegisteredEntity>(arguments, async context =>
            {
                var client = context.Client;
                var result = await client.SearchAllAsync<IRegisteredEntity>(s => s
                    .Query(qcd => qcd.Bool(bq => bq.Filter(
                        fq => fq.Term(r => r.IndexName, searchType.IndexName))))
                    .Index(IndexName(SearchTypes.RegisteredEntity)));

                return new IndexQueryHits<IRegisteredEntity>()
                {
                    Hits = result.SelectMany(s => s.Hits.Select(h => h.Source)).ToList(),
                    Total = result.FirstOrDefault()?.Total ?? 0
                };
            });
        }

        public Task<IndexQueryHitsResponse<T>> GetLeftOnlyEntitiesAsync<T>(
            SearchType<T> searchType,
            string leftName,
            string rightName)
            where T : class, ISearchEntity
        {
            return UseClient<T>(new ContextCodexArgumentsBase() { RepositoryScopeId = leftName }, async leftContext =>
            {
                var r = await UseClient<T>(new ContextCodexArgumentsBase() { RepositoryScopeId = rightName }, async rightContext =>
                {
                    var client = leftContext.Client;

                    var result = await client.SearchAllAsync<T>(s => s
                        .Query(qcd => qcd.Bool(bq => bq
                            .MustNot(q1 => q1.StoredFilterQuery(rightContext, IndexName(searchType)))
                            .Filter(q1 => q1.StoredFilterQuery(leftContext, IndexName(searchType)))
                            ))
                        .Index(IndexName(searchType))
                        .Type(searchType.Type));

                    return new IndexQueryHits<T>()
                    {
                        Hits = result.SelectMany(s => s.Hits.Select(h => h.Source)).ToList(),
                        Total = result.FirstOrDefault()?.Total ?? 0
                    };

                });

                return r.Result;
            });
        }

        public Task<IndexQueryHitsResponse<ISearchEntity>> GetSearchEntityInfoAsync(SearchType searchType)
        {
            var arguments = new ContextCodexArgumentsBase();
            return UseClient<ISearchEntity>(arguments, async context =>
            {
                var client = context.Client;

                await client.RefreshAsync(IndexName(searchType));

                var result = await client.SearchAllAsync<ISearchEntity>(s => s
                    .Query(qcd => qcd.MatchAll())
                    .Index(IndexName(searchType))
                    .Source(sf => sf.Includes(f => f.Fields(e => e.Uid, e => e.EntityVersion, e => e.StableId)))
                    .Type(searchType.Type));

                return new IndexQueryHits<ISearchEntity>()
                {
                    Hits = result.SelectMany(s => s.Hits.Select(h => h.Source)).ToList(),
                    Total = result.FirstOrDefault()?.Total ?? 0
                };
            });
        }

        private async Task<IndexQueryResponse<T>> UseClientSingle<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext, Task<T>> useClient)
        {
            var elasticResponse = await Service.UseClient(async context =>
            {
                try
                {
                    var sfContext = await GetStoredFilterContextAsync(arguments, context);
                    var result = await useClient(sfContext);
                    return new IndexQueryResponse<T>()
                    {
                        Result = result
                    };
                }
                catch (Exception ex)
                {
                    return new IndexQueryResponse<T>()
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
