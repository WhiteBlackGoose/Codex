using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Search;
using Codex.Storage;
using Codex.Utilities;

namespace Codex.ElasticSearch.Legacy.Bridge
{
    /// <summary>
    /// ICodexStore implementation using legacy ElasticsearchStorage
    /// </summary>
    public class LegacyElasticSearchCodex : ICodex
    {
        private Stopwatch watch = Stopwatch.StartNew();
        private ElasticsearchStorage Storage { get; }
        private LegacyElasticSearchStoreConfiguration Configuration { get; }

        public LegacyElasticSearchCodex(LegacyElasticSearchStoreConfiguration configuration)
        {
            Configuration = configuration;
            Storage = new ElasticsearchStorage(configuration.Endpoint);
        }

        public Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            return QueryHits(async storage =>
            {
                var searchTerm = arguments.SearchString;
                var searchRepos = arguments.GetSearchRepos();
                
                if (arguments.SearchString.StartsWith("`"))
                {
                    var results = await Storage.TextSearchAsync(searchRepos, searchTerm.TrimStart('`'), arguments.MaxResults);
                    return new IndexQueryHits<ISearchResult>()
                    {
                        Hits = results.Select(t => new SearchResult()
                        {
                            TextLine = new TextLineSpanResult()
                            {
                                TextSpan = t.Span,
                                ProjectId = t.ReferringProjectId,
                                ProjectRelativePath = t.ReferringFilePath,
                            }
                        }).OfType<ISearchResult>().ToList(),
                        Total = results.Count
                    };
                }
                else
                {
                    var result = await storage.SearchAsync(searchRepos, searchTerm, maxNumberOfItems: arguments.MaxResults);

                    return new IndexQueryHits<ISearchResult>()
                    {
                        Hits = result.Entries.Select(t => new SearchResult()
                        {
                            Definition = new DefinitionSymbol(t.Symbol)
                        }).OfType<ISearchResult>().ToList(),
                        Total = result.Total
                    };
                }
            });
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            return Query(async storage =>
            {
                var searchRepos = arguments.GetSearchRepos();

                var definitionResult = await ((IStorage)Storage).GetDefinitionsAsync(searchRepos, arguments.ProjectId, arguments.SymbolId);
                var definitionSpan = definitionResult?.FirstOrDefault()?.Span;
                var definition = definitionSpan?.Definition;
                var symbolName = definition?.ShortName ?? arguments.SymbolId;

                var result = new ReferencesResult()
                {
                    SymbolDisplayName = symbolName,
                    ProjectId = arguments.ProjectId,
                    SymbolId = arguments.SymbolId
                };

                var referencesResult = await Storage.GetReferencesToSymbolAsync(
                    searchRepos,
                    new ReferenceSymbol()
                    {
                        ReferenceKind = arguments.ReferenceKind,
                        ProjectId = arguments.ProjectId,
                        Id = SymbolId.UnsafeCreateWithValue(arguments.SymbolId),
                    }.SetProjectScope(arguments.ProjectScopeId));

                if (referencesResult.Entries.Count != 0 && arguments.ReferenceKind == null)
                {
                    if (definition != null)
                    {
                        if (arguments.ProjectScopeId == null)
                        {
                            var relatedDefinitions = await Storage.GetRelatedDefinitions(searchRepos,
                                definition.Id.Value,
                                definition.ProjectId);

                            result.RelatedDefinitions.AddRange(relatedDefinitions.Select(s => s.Symbol));
                        }
                        else
                        {
                            var definitionReferences = await Storage.GetReferencesToSymbolAsync(
                                searchRepos,
                                new ReferenceSymbol()
                                {
                                    ProjectId = arguments.ProjectId,
                                    Id = SymbolId.UnsafeCreateWithValue(arguments.SymbolId),
                                    ReferenceKind = nameof(ReferenceKind.Definition)
                                });

                            referencesResult.Entries.InsertRange(0, definitionReferences.Entries);
                        }
                    }
                }

                result.Total = referencesResult.Total;
                result.Hits.AddRange(referencesResult.Entries.Select(e => new ReferenceSearchResult()
                {
                    ProjectId = e.ReferringProjectId,
                    ReferenceSpan = e.ReferringSpan,
                    ProjectRelativePath = e.ReferringFilePath,
                }));

                return result;
            });
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        public async Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            arguments.ReferenceKind = nameof(ReferenceKind.Definition);
            var result = await FindAllReferencesAsync(arguments);
            if (result.Error != null || result.Result.Total != 0 || !arguments.FallbackFindAllReferences)
            {
                return result;
            }

            arguments.ReferenceKind = null;
            return await FindAllReferencesAsync(arguments);
        }

        public Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            return Query<IBoundSourceFile>(async storage =>
            {
                var searchRepos = arguments.GetSearchRepos();

                var boundSourceFile = await storage.GetBoundSourceFileAsync(searchRepos, arguments.ProjectId, arguments.ProjectRelativePath, arguments.DefinitionOutline);
                if (boundSourceFile != null && !arguments.DefinitionOutline)
                {
                    var projectContents = await storage.GetProjectContentsAsync(searchRepos, arguments.ProjectId);
                    if (projectContents != null)
                    {
                        boundSourceFile.Commit = new Commit()
                        {
                            CommitId = boundSourceFile.IndexName,
                            DateUploaded = projectContents.DateUploaded
                        };
                    }
                }

                return boundSourceFile;
            });
        }

        public Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments)
        {
            return Query(async storage =>
            {
                var projectContents = await storage.GetProjectContentsAsync(arguments.GetSearchRepos(), arguments.ProjectId);
                var referencingProjects = await Storage.GetReferencingProjects(arguments.ProjectId);

                var project = new AnalyzedProject();
                project.ProjectReferences.AddRange(projectContents.References);
                project.Files.AddRange(projectContents.Files.Select(f => new ProjectFileLink(f)));

                return new GetProjectResult()
                {
                    DateUploaded = projectContents.DateUploaded,
                    Project = project,
                    ReferencingProjects = referencingProjects.ToList()
                };
            });
        }

        private async Task<IndexQueryHitsResponse<T>> QueryHits<T>(Func<IStorage, Task<IndexQueryHits<T>>> query)
        {
            var start = watch.Elapsed;

            var result = await query(Storage);

            return new IndexQueryHitsResponse<T>()
            {
                Result = result,
                Duration = watch.Elapsed - start,
            };
        }

        private async Task<IndexQueryResponse<T>> Query<T>(Func<IStorage, Task<T>> query)
        {
            var start = watch.Elapsed;

            var result = await query(Storage);

            return new IndexQueryResponse<T>()
            {
                Result = result,
                Duration = watch.Elapsed - start,
            };
        }
    }

    internal static class LegacyElasticSearchCodexHelpers
    {
        public static string[] GetSearchRepos(this ContextCodexArgumentsBase arguments)
        {
            if (arguments.RepositoryScopeId == null)
            {
                return new string[0];
            }

            return new[] { arguments.RepositoryScopeId };
        }
    }
}
