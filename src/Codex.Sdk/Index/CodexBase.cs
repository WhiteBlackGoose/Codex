using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using System.Threading;
using Codex.Sdk.Utilities;
using Codex.Search;
using static Codex.Search.SearchUtilities;

namespace Codex.Search
{
    public class ClientContext<TClient>
        where TClient : IClient
    {
        // TODO: Disable
        public bool CaptureRequests = true;
        public TClient Client;
        public List<string> Requests;

        public ClientContext()
        {
            Requests = new List<string>();
        }

        public ClientContext(ClientContext<TClient> other)
        {
            CaptureRequests = other.CaptureRequests;
            Client = other.Client;
            Requests = other.Requests;
        }
    }

    public class StoredFilterSearchContext<TClient> : ClientContext<TClient>, IStoredFilterInfo
        where TClient : IClient
    {
        public string RepositoryScopeId { get; }

        public string StoredFilterIndexName { get; }

        public string StoredFilterUidPrefix { get; }

        public StoredFilterSearchContext(ClientContext<TClient> context, string repositoryScopeId, string storedFilterIndexName, string storedFilterUidPrefix)
            : base(context)
        {
            RepositoryScopeId = repositoryScopeId;
            StoredFilterIndexName = storedFilterIndexName;
            StoredFilterUidPrefix = storedFilterUidPrefix;
        }
    }

    public class CodexBaseConfiguration
    {
        public TimeSpan CachedAliasIdRetention = TimeSpan.FromMinutes(30);
    }

    public abstract class CodexBase<TClient, TConfiguration> : ICodex
       where TClient : IClient
       where TConfiguration : CodexBaseConfiguration
    {
        internal readonly TConfiguration Configuration;

        private Mappings m;

        private ConcurrentDictionary<string, (DateTime resolveTime, string repositorySnapshotId)> resolvedRepositoryIds = new ConcurrentDictionary<string, (DateTime resolveTime, string repositorySnapshotId)>();

        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return FindReferencesCore(arguments, async context =>
            {
                var client = context.Client;
                IIndexSearchResponse<IReferenceSearchModel> referencesResult = await FindAllReferencesAsyncCore(context, arguments);

                return referencesResult;
            });
        }

        private async Task<IIndexSearchResponse<IReferenceSearchModel>> FindAllReferencesAsyncCore(StoredFilterSearchContext<TClient> context, FindAllReferencesArguments arguments)
        {
            return await context.Client.ReferenceIndex.SearchAsync(
                context,
                cq =>
                    cq.Term(m.Reference.Reference.ProjectId, arguments.ProjectId)
                    & cq.Term(m.Reference.Reference.Id, Placeholder.Value<SymbolId>()),
                sort: m.Reference.ProjectId,
                take: arguments.MaxResults);
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
                var referencesResult = await context.Client.ReferenceIndex.SearchAsync(
                    context,
                    cq =>
                        cq.Term(m.Reference.Reference.ProjectId, arguments.ProjectId)
                        & cq.Term(m.Reference.Reference.Id, Placeholder.Value<SymbolId>())
                        & cq.Term(m.Reference.Reference.ReferenceKind, nameof(ReferenceKind.Definition)),
                    take: arguments.MaxResults);

                if (arguments.FallbackFindAllReferences && referencesResult.Total == 0)
                {
                    // No definitions, return the the result of find all references 
                    referencesResult = await FindAllReferencesAsyncCore(context, arguments);
                }

                return referencesResult;
            });
        }

        private async Task<IndexQueryResponse<ReferencesResult>> FindReferencesCore(FindSymbolArgumentsBase arguments, Func<StoredFilterSearchContext<TClient>, Task<IIndexSearchResponse<IReferenceSearchModel>>> getReferencesAsync)
        {
            return await UseClientSingle(arguments, async context =>
            {
                var client = context.Client;

                IIndexSearchResponse<IReferenceSearchModel> referencesResult = await getReferencesAsync(context);

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
                    Total = referencesResult.Total,
                    // TODO: Set RelatedDefinitions
                };
            });
        }

        private async Task<string> GetSymbolShortName(StoredFilterSearchContext<TClient> context, FindSymbolArgumentsBase arguments)
        {
            var client = context.Client;
            var definitionsResult = await client.DefinitionIndex.SearchAsync(
                    context,
                    cq =>
                        cq.Term(m.Definition.Definition.ProjectId, arguments.ProjectId)
                        & cq.Term(m.Definition.Definition.Id, Placeholder.Value<SymbolId>()),
                    take: 1);

            return definitionsResult.Hits.FirstOrDefault()?.Source.Definition.ShortName;
        }

        public async Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            // TODO: Get text source if bound source is unavailable.
            return await UseClientSingle<IBoundSourceFile>(arguments, async context =>
            {
                var client = context.Client;

                //var boundResults = await client.SearchAsync<BoundSourceSearchModel>(sd => sd
                //    .StoredFilterSearch(context, IndexName(SearchTypes.BoundSource), qcd => qcd.Bool(bq => bq.Filter(
                //            fq => fq.Term(s => s.File.Info.ProjectId, arguments.ProjectId),
                //            fq => fq.Term(s => s.File.Info.ProjectRelativePath, arguments.ProjectRelativePath))))
                //    .Take(1))
                //.ThrowOnFailure();

                var boundResults = await client.BoundSourceIndex.QueryAsync<BoundSourceSearchModel>(
                    context,
                    cq =>
                         cq.Term(m.BoundSource.File.Info.ProjectId, arguments.ProjectId)
                         & cq.Term(m.BoundSource.File.Info.ProjectRelativePath, arguments.ProjectRelativePath),
                    take: 1);

                if (boundResults.Hits.Count != 0)
                {
                    var boundSearchModel = boundResults.Hits.First().Source;
                    //var textResults = await client.GetAsync<TextSourceSearchModel>(boundSearchModel.TextUid,
                    //    gd => gd.Index(Configuration.Prefix + SearchTypes.TextSource.IndexName)
                    //            .Routing(GetRouting(boundSearchModel.TextUid)))
                    //.ThrowOnFailure();

                    var chunks = await client.TextChunkIndex.GetAsync(context, boundSearchModel.File.Chunks.SelectArray(c => c.Id));

                    var repoResults = await client.RepositoryIndex.QueryAsync<RepositorySearchModel>(
                        context,
                        cq => cq.Term(m.Repository.Repository.Name, boundSearchModel.File.Info.RepositoryName),
                        take: 1);

                    var commitResults = await client.CommitIndex.QueryAsync<CommitSearchModel>(
                        context,
                        cq => cq.Term(m.Commit.Commit.RepositoryName, boundSearchModel.File.Info.RepositoryName),
                        take: 1);

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

                //var response = await client.SearchAsync<ProjectSearchModel>(sd => sd
                //    .StoredFilterSearch(context, IndexName(SearchTypes.Project), qcd => qcd.Bool(bq => bq.Filter(
                //            fq => fq.Term(s => s.Project.ProjectId, arguments.ProjectId))))
                //    .Take(1))
                //.ThrowOnFailure();

                var response = await client.ProjectIndex.SearchAsync(
                    context,
                    cq =>
                         cq.Term(m.Project.Project.ProjectId, arguments.ProjectId),
                    take: 1);

                if (response.Hits.Count != 0)
                {
                    IProjectSearchModel projectSearchModel = response.Hits.First().Source;

                    // TODO: Not sure why this is a good marker for when the project was uploaded. Since project search model may be deduplicated,
                    // to a past result we probably need something more accurate. Maybe the upload date of the stored filter. That would more closely
                    // match the legacy behavior.
                    //var commitResponse = await client.SearchAsync<ICommitSearchModel>(sd => sd
                    //     .StoredFilterSearch(context, IndexName(SearchTypes.Commit), qcd => qcd.Bool(bq => bq.Filter(
                    //             fq => fq.Term(s => s.Commit.RepositoryName, projectSearchModel.Project.RepositoryName))))
                    //     .Take(1)
                    //     .CaptureRequest(context));

                    var commitResponse = await client.CommitIndex.SearchAsync(
                        context,
                        cq => cq.Term(m.Commit.Commit.RepositoryName, projectSearchModel.Project.RepositoryName),
                        take: 1);

                    var referencesResult = await client.ProjectReferenceIndex.SearchAsync(
                        context,
                        cq => cq.Term(m.ProjectReference.ProjectReference.ProjectId, arguments.ProjectId),
                        sort: m.ProjectReference.ProjectId,
                        take: arguments.MaxResults);

                    //var referencesResult = await client.SearchAsync<IProjectReferenceSearchModel>(s => s
                    //    .StoredFilterSearch(context, IndexName(SearchTypes.ProjectReference), qcd => qcd.Bool(bq => bq.Filter(
                    //        fq => fq.Term(r => r.ProjectReference.ProjectId, arguments.ProjectId))))
                    //    .Sort(sd => sd.Ascending(r => r.ProjectId))
                    //    .Source(sfd => sfd.Includes(f => f.Field(r => r.ProjectId)))
                    //    .Take(arguments.MaxResults))
                    //.ThrowOnFailure();

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

                var client = context.Client;

                if (!arguments.TextSearch)
                {
                    var terms = searchPhrase.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    bool allowReferencedDefinitions = terms.Any(t => IsAllowReferenceDefinitionsTerm(t)) || arguments.AllowReferencedDefinitions;
                    terms = terms.Where(t => !IsAllowReferenceDefinitionsTerm(t)).ToArray();

                    var definitionsResult = await client.DefinitionIndex.SearchAsync(
                        context,
                        cq => GetTermsFilter(cq, terms),
                        boost: cq => GetTermsFilter(cq, terms, boostOnly: true),
                        //filterIndexName: allowReferencedDefinitions ? indexName : GetDeclaredDefinitionsIndexName(indexName)
                        take: arguments.MaxResults);

                    //var indexName = IndexName(SearchTypes.Definition);

                    //var definitionsResult = await client.SearchAsync<IDefinitionSearchModel>(s => s
                    //        .StoredFilterSearch(context, indexName, qcd => qcd.Bool(bq => bq
                    //            .Filter(GetTermsFilter(terms, allowReferencedDefinitions: allowReferencedDefinitions))
                    //            .Should(GetTermsFilter(terms, boostOnly: true))),
                    //            filterIndexName: allowReferencedDefinitions ? indexName : GetDeclaredDefinitionsIndexName(indexName))
                    //        .Take(arguments.MaxResults))
                    //    .ThrowOnFailure();

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
                //var textChunkResults = await client.SearchAsync<ITextChunkSearchModel>(
                //     s => s
                //         .StoredFilterSearch(context, IndexName(SearchTypes.TextChunk), f =>
                //             f.Bool(bq =>
                //             bq.Must(qcd => qcd.ConfigureIfElse(isPrefix,
                //                 f0 => f0.MatchPhrasePrefix(mpp => mpp.Field(sf => sf.Chunk.ContentLines).Query(searchPhrase).MaxExpansions(100)),
                //                 f0 => f0.MatchPhrase(mpp => mpp.Field(sf => sf.Chunk.ContentLines).Query(searchPhrase))))))
                //         .Highlight(h => h.Fields(hf => hf.Field(sf => sf.Chunk.ContentLines).BoundaryCharacters("\n\r")))
                //         .Take(arguments.MaxResults))
                //     .ThrowOnFailure();

                Placeholder.Todo("Add highlighting support");
                var textChunkResults = await client.TextChunkIndex.SearchAsync(
                    context,
                    cq => isPrefix 
                        ? cq.MatchPhrasePrefix(m.TextChunk.Chunk.ContentLines, searchPhrase, maxExpansions: 100)
                        : cq.MatchPhrase(m.TextChunk.Chunk.ContentLines, searchPhrase),
                    take: arguments.MaxResults);

                var chunkIds = textChunkResults.Hits.ToDictionarySafe(s => s.Source.EntityContentId);

                var textResults = await client.TextSourceIndex.SearchAsync(
                    context,
                    cq => cq.Terms(m.TextSource.File.Chunks.Id, chunkIds.Keys),
                    take: arguments.MaxResults);

                TextLineSpan withOffset(TextLineSpan span, int startLineNumber)
                {
                    span.LineNumber += startLineNumber;
                    return span;
                }
                
                var sourceFileResults =
                   (from hit in textResults.Hits
                    from chunkHit in hit.Source.File.Chunks.Select(c => (hit: chunkIds.GetOrDefault(c.Id), c.StartLineNumber)).Where(h => h.hit != null)
                    from highlight in chunkHit.hit.Highlights
                    select new SearchResult()
                    {
                        TextLine = new TextLineSpanResult(hit.Source.File.Info)
                        {
                            TextSpan = withOffset(highlight, chunkHit.StartLineNumber)
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

        //private Func<CodexQueryBuilder<IDefinitionSearchModel>, CodexQuery<IDefinitionSearchModel>> GetTermsFilter(
        //    string[] terms,
        //    bool boostOnly = false,
        //    bool allowReferencedDefinitions = false)
        //{
        //    return qcd => qcd.Bool(bq => bq.Filter(GetTermsFilters(terms, boostOnly, allowReferencedDefinitions)));
        //}

        private CodexQuery<IDefinitionSearchModel> GetTermsFilter(
            CodexQueryBuilder<IDefinitionSearchModel> cq,
            string[] terms,
            bool boostOnly = false)
        {
            CodexQuery<IDefinitionSearchModel> query = default;
            foreach (var term in terms)
            {
                query &= ApplyTermFilter(term.ToLowerInvariant(), cq, boostOnly);
            }

            if (!boostOnly)
            {
                query&= !cq.Term(m.Definition.Definition.ExcludeFromDefaultSearch, true);
            }

            return query;
        }

        private CodexQuery<IDefinitionSearchModel> KeywordFilter(string term, CodexQueryBuilder<IDefinitionSearchModel> fq)
        {
            Placeholder.Todo("Why two different keywords fields?");
            return fq.Term(m.Definition.Keywords, term.ToLowerInvariant())
                    | fq.Term(m.Definition.Definition.Keywords, term.ToLowerInvariant());
        }

        private CodexQuery<IDefinitionSearchModel> NameFilter(string term, CodexQueryBuilder<IDefinitionSearchModel> fq, bool boostOnly)
        {
            var terms = term.CreateNameTerm();

            if (boostOnly)
            {
                return fq.Term(m.Definition.Definition.ShortName, terms.ExactNameTerm.ToLowerInvariant());
            }
            else
            {
                return NameFilterCore(fq, terms);
            }
        }

        private CodexQuery<IDefinitionSearchModel> NameFilterCore(CodexQueryBuilder<IDefinitionSearchModel> fq, QualifiedNameTerms terms)
        {
            return fq.Term(m.Definition.Definition.ShortName, terms.NameTerm)
                | fq.Term(m.Definition.Definition.ShortName, terms.SecondaryNameTerm)
                | fq.Term(m.Definition.Definition.AbbreviatedName, terms.RawNameTerm);
        }

        private CodexQuery<IDefinitionSearchModel> QualifiedNameTermFilters(string term, CodexQueryBuilder<IDefinitionSearchModel> fq)
        {
            var terms = ParseContainerAndName(term);

            Placeholder.Todo("Bring back this logic?");
            // TEMPORARY HACK: This is needed due to the max length placed on container terms
            // The analyzer should be changed to use path_hierarchy with reverse option
            //if ((terms.ContainerTerm.Length > (CustomAnalyzers.MaxGram - 2)) && terms.ContainerTerm.Contains("."))
            //{
            //    terms.ContainerTerm = terms.ContainerTerm.SubstringAfterFirstOccurrence('.');
            //}

            return NameFilterCore(fq, terms) 
                & fq.Term(m.Definition.Definition.ContainerQualifiedName, terms.ContainerTerm);
        }

        private CodexQuery<IDefinitionSearchModel> IndexTermFilters(string term, CodexQueryBuilder<IDefinitionSearchModel> fq)
        {
            return Placeholder.Value<CodexQuery<IDefinitionSearchModel>>("Determine how index queries will be represented? Probably as a stored filter");
            //return fq.Term("_index", term.ToLowerInvariant());
        }

        private CodexQuery<IDefinitionSearchModel> ApplyTermFilter(string term, CodexQueryBuilder<IDefinitionSearchModel> fq, bool boostOnly)
        {
            var d = NameFilter(term, fq, boostOnly);

            if (!boostOnly)
            {
                // Advanced case where symbol id is mentioned as a term
                d |= QualifiedNameTermFilters(term, fq);
                d |= IndexTermFilters(term, fq);
                d |= KeywordFilter(term, fq);
                d |= fq.Term(m.Definition.Definition.Id, Placeholder.Value<SymbolId>(term.ToLowerInvariant()));
                d |= fq.Term(m.Definition.Definition.ProjectId, term);
                d |= fq.Term(m.Definition.Definition.Kind, term);
            }

            return d;
        }

        protected abstract Task<StoredFilterSearchContext<TClient>> GetStoredFilterContextAsync(ContextCodexArgumentsBase arguments, ClientContext<TClient> context);

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

        protected abstract Task<IndexQueryHitsResponse<T>> UseClient<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext<TClient>, Task<IndexQueryHits<T>>> useClient);

        protected abstract Task<IndexQueryResponse<T>> UseClientSingle<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext<TClient>, Task<T>> useClient);
    }
}
