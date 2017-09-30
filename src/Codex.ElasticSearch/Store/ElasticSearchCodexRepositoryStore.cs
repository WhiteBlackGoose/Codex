using Codex.Analysis;
using Codex.Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;
using Codex.Storage.DataModel;
using Codex.Storage.Utilities;
using System.Collections.Concurrent;

namespace Codex.ElasticSearch
{
    class ElasticSearchCodexRepositoryStore : ICodexRepositoryStore
    {
        private readonly ElasticSearchStore store;
        private readonly ElasticSearchBatcher batcher;
        private readonly IRepository repository;
        private readonly ICommit commit;
        private readonly ConcurrentDictionary<string, CommitFileLink> commitFilesByRepoRelativePath = new ConcurrentDictionary<string, CommitFileLink>(StringComparer.OrdinalIgnoreCase);

        public ElasticSearchCodexRepositoryStore(ElasticSearchStore store, IRepository repository, ICommit commit)
        {
            this.store = store;
            this.repository = repository;
            this.commit = commit;
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
                await batcher.AddAsync(store.ProjectStore, new ProjectSearchModel() { Project = project });

                foreach (var projectReference in project.ProjectReferences)
                {
                    batcher.Add(store.ProjectReferenceStore, new ProjectReferenceSearchModel(project)
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
                var textModel = new TextSourceSearchModel()
                {
                    // TODO: This should probably be handled by custom serializer
                    File = file.EnableFullTextSearch()
                };

                await batcher.AddAsync(store.TextSourceStore, textModel);
                UpdateCommitFile(textModel);
                AddProperties(textModel, file.Info.Properties);
            }
        }

        private void AddDefinitions(IEnumerable<DefinitionSymbol> definitions, bool declared)
        {
            Placeholder.Todo("Add declared/reference definitions to separate stored filters");
            foreach (var definition in definitions)
            {
                batcher.Add(store.DefinitionStore, new DefinitionSearchModel()
                {
                    Definition = definition,
                }, 
                // If definition is declared in this code base, add it to declared def filter for use when boosting or searching
                // only definitions that have source associated with them
                declared ? batcher.DeclaredDefinitionStoredFilter : batcher.EmptyStoredFilters);
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
                batcher.Add(store.PropertyStore, new PropertySearchModel()
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

            boundSourceFile.RepositoryName = boundSourceFile.RepositoryName ?? sourceFileInfo.RepositoryName;
            boundSourceFile.RepoRelativePath = boundSourceFile.RepoRelativePath ?? sourceFileInfo.RepoRelativePath;

            // TODO: These properties should not be defined on ISourceFileInfo as they require binding information
            boundSourceFile.Language = boundSourceFile.Language ?? sourceFileInfo.Language;
            boundSourceFile.ProjectRelativePath = boundSourceFile.ProjectRelativePath ?? sourceFileInfo.ProjectRelativePath;

            var textModel = new TextSourceSearchModel()
            {
                // TODO: This should probably be handled by custom serializer
                File = boundSourceFile.SourceFile.EnableFullTextSearch(),
            };

            batcher.Add(store.TextSourceStore, textModel);
            UpdateCommitFile(textModel);

            var boundSourceModel = new BoundSourceSearchModel()
            {
                BindingInfo = boundSourceFile,
                TextUid = textModel.Uid,
                CompressedClassifications = new ClassificationListModel(boundSourceFile.Classifications)
            };

            await batcher.AddAsync(store.BoundSourceStore, boundSourceModel, onAdded: () =>
            {
                AddBoundSourceFileAssociatedData(boundSourceFile, boundSourceModel);
            });
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

            AddDefinitions(boundSourceFile.Definitions.Select(ds => ds.Definition).Where(d => !d.ExcludeFromSearch), declared: true);

            var referenceLookup = boundSourceFile.References
                .Where(r => !(r.Reference.ExcludeFromSearch))
                .ToLookup(r => r.Reference, ReferenceListModel.ReferenceSymbolEqualityComparer);

            foreach (var referenceGroup in referenceLookup)
            {
                var referenceModel = new ReferenceSearchModel((IProjectFileScopeEntity)boundSourceFile)
                {
                    Reference = new Symbol(referenceGroup.Key),
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

                batcher.Add(store.ReferenceStore, referenceModel);
            }
        }

        public async Task FinalizeAsync()
        {
            await batcher.AddAsync(store.CommitFilesStore, new CommitFilesSearchModel(this.commit)
            {
                CommitFiles = commitFilesByRepoRelativePath.Values.OrderBy(cf => cf.RepoRelativePath, StringComparer.OrdinalIgnoreCase).ToList()
            });

            await batcher.FinalizeAsync();
        }
    }
}