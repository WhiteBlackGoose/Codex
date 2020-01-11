using Codex.Analysis;
using Codex.Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;
using Codex.Storage.Utilities;
using System.Collections.Concurrent;
using Codex.Logging;
using Codex.Sdk.Utilities;
using Codex.ElasticSearch.Utilities;
using Codex.Storage.DataModel;

namespace Codex.ElasticSearch
{
    public abstract class IndexingCodexRepositoryStoreBase<TStoredFilterBuilder> : ICodexRepositoryStore
    {
        /// <summary>
        /// Empty set of stored filters for passing as additional stored filters
        /// </summary>
        internal readonly TStoredFilterBuilder[] EmptyStoredFilters = Array.Empty<TStoredFilterBuilder>();

        protected IBatcher<TStoredFilterBuilder> Batcher { get; }
        private readonly Repository repository;
        private readonly Commit commit;
        private readonly Branch branch;
        private readonly ConcurrentDictionary<string, CommitFileLink> commitFilesByRepoRelativePath = new ConcurrentDictionary<string, CommitFileLink>(StringComparer.OrdinalIgnoreCase);
        internal Logger Logger { get; }

        public IndexingCodexRepositoryStoreBase(IBatcher<TStoredFilterBuilder> batcher, Logger logger, Repository repository, Commit commit, Branch branch)
        {
            Batcher = batcher;
            Logger = logger;
            this.repository = repository;
            this.commit = commit;
            this.branch = branch;

            Batcher.Add(SearchTypes.Repository, new RepositorySearchModel()
            {
                Repository = repository,
            });

            if (commit != null)
            {
                Batcher.Add(SearchTypes.Commit, new CommitSearchModel()
                {
                    Commit = commit,
                });
            }

            if (branch != null)
            {
                Placeholder.Todo("Add branch store and add branch to branch store");
            }

            Placeholder.Todo("Add commit bound source document (with links to changed files in commit, commit stats [lines added/removed], link to commit portal, link to diff view).");
        }

        public async Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            foreach (var file in files)
            {
                await AddBoundSourceFileAsync(file);
            }
        }

        public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
        {
            foreach (var link in links)
            {
                commitFilesByRepoRelativePath.TryAdd(link.RepoRelativePath, link);
            }

            return Task.CompletedTask;
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
        {
            return Placeholder.NotImplementedAsync("Add language support");
        }

        public async Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
        {
            foreach (var project in projects)
            {
                await Batcher.AddAsync(SearchTypes.Project, new ProjectSearchModel() { Project = project });

                await AddBoundFilesAsync(project.AdditionalSourceFiles);

                foreach (var projectReference in project.ProjectReferences)
                {
                    Batcher.Add(SearchTypes.ProjectReference, new ProjectReferenceSearchModel(project)
                    {
                        ProjectReference = projectReference
                    });

                    AddDefinitions(projectReference.Definitions, declared: false);
                }
            }
        }

        public async Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
        {
            foreach (var file in files)
            {
                TextSourceSearchModel textModel = await AddTextFileAsync(file);

                UpdateCommitFile(textModel);
                AddProperties(textModel, file.Info.Properties);
            }
        }

        private async Task<TextSourceSearchModel> AddTextFileAsync(SourceFile file)
        {
            TextIndexingUtilities.ToChunks(file, file.ExcludeFromSearch, out var chunkFile, out var chunks, encodeFullText: true);
            var textModel = new TextSourceSearchModel()
            {
                File = chunkFile
            };

            await Batcher.AddAsync(SearchTypes.TextSource, textModel);
            foreach (var chunk in chunks)
            {
                await Batcher.AddAsync(SearchTypes.TextChunk, chunk);
            }

            return textModel;
        }

        private void AddDefinitions(IEnumerable<DefinitionSymbol> definitions, bool declared)
        {
            foreach (var definition in definitions)
            {
                if (definition.ExcludeFromSearch)
                {
                    // Definitions must be stored even if not contributing to search to allow
                    // other operations like tooltips/showing symbol name for find all references
                    // so we just set ExcludeFromDefaultSearch to true
                    definition.ExcludeFromDefaultSearch = true;
                }

                Batcher.Add(SearchTypes.Definition, new DefinitionSearchModel()
                {
                    Definition = definition,
                }, 
                // If definition is declared in this code base, add it to declared def filter for use when boosting or searching
                // only definitions that have source associated with them
                declared ? Batcher.DeclaredDefinitionStoredFilter : EmptyStoredFilters);
            }
        }

        private void AddProperties(ISearchEntity entity, PropertyMap properties)
        {
            if (properties == null)
            {
                return;
            }

            foreach (var property in properties)
            {
                Batcher.Add(SearchTypes.Property, new PropertySearchModel()
                {
                    Key = property.Key,
                    Value = property.Value,
                    OwnerId = entity.Uid,
                });
            }
        }

        public async Task AddBoundSourceFileAsync(BoundSourceFile boundSourceFile)
        {
            var sourceFileInfo = boundSourceFile.SourceFile.Info;
            Logger.LogDiagnosticWithProvenance($"{sourceFileInfo.ProjectId}:{sourceFileInfo.ProjectRelativePath}");
            boundSourceFile.ApplySourceFileInfo();

            Placeholder.Todo($"Get {nameof(ISourceControlFileInfo.SourceControlContentId)} from source control provider during analysis");

            TextSourceSearchModel textModel = await AddTextFileAsync(boundSourceFile.SourceFile);

            Batcher.Add(SearchTypes.TextSource, textModel);
            Logger.LogDiagnosticWithProvenance($"[Text#{textModel.Uid}] {sourceFileInfo.ProjectId}:{sourceFileInfo.ProjectRelativePath}");

            UpdateCommitFile(textModel);

            var boundSourceModel = new BoundSourceSearchModel(textModel)
            {
                BindingInfo = boundSourceFile,
                TextUid = textModel.Uid,
                CompressedClassifications = ClassificationListModel.CreateFrom(boundSourceFile.Classifications),
                CompressedReferences = ReferenceListModel.CreateFrom(boundSourceFile.References)
            };

            await Batcher.AddAsync(SearchTypes.BoundSource, boundSourceModel);

            AddBoundSourceFileAssociatedData(boundSourceFile, boundSourceModel);
            Logger.LogDiagnosticWithProvenance($"[Bound#{boundSourceModel.Uid}|Text#{textModel.Uid}] {sourceFileInfo.ProjectId}:{sourceFileInfo.ProjectRelativePath}");
        }

        private void UpdateCommitFile(TextSourceSearchModel sourceSearchModel)
        {
            CommitFileLink commitFileLink;
            if (commitFilesByRepoRelativePath.TryGetValue(sourceSearchModel.File.Info.RepoRelativePath, out commitFileLink))
            {
                commitFileLink.FileId = sourceSearchModel.Uid;
            }
        }

        private void AddBoundSourceFileAssociatedData(BoundSourceFile boundSourceFile, BoundSourceSearchModel boundSourceModel)
        {
            AddProperties(boundSourceModel, boundSourceFile.SourceFile.Info.Properties);

            AddDefinitions(boundSourceFile.Definitions.Select(ds => ds.Definition), declared: true);

            var referenceLookup = boundSourceFile.References
                .Where(r => !(r.Reference.ExcludeFromSearch))
                .ToLookup(r => r.Reference, ReferenceListModel.ReferenceSymbolEqualityComparer);

            foreach (var referenceGroup in referenceLookup)
            {
                var referenceModel = new ReferenceSearchModel(Requires.Expect<IProjectFileScopeEntity>(boundSourceFile.SourceFile.Info))
                {
                    Reference = referenceGroup.Key,
                };

                var spanList = referenceGroup.AsReadOnlyList();

                if (referenceGroup.Count() < 10)
                {
                    // Small number of references, just store simple list
                    referenceModel.Spans = spanList;
                }
                else
                {
                    referenceModel.CompressedSpans = new SymbolLineSpanListModel(spanList);
                }

                Batcher.Add(SearchTypes.Reference, referenceModel);
            }
        }

        public async Task FinalizeAsync()
        {
            await Batcher.AddAsync(SearchTypes.CommitFiles, new CommitFilesSearchModel(this.commit)
            {
                CommitFiles = commitFilesByRepoRelativePath.Values.OrderBy(cf => cf.RepoRelativePath, StringComparer.OrdinalIgnoreCase).ToList()
            });

            await Batcher.FinalizeAsync(repository.Name);
        }
    }
}