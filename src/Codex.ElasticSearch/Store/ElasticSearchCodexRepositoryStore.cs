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
        private readonly ElasticSearchBatch batch;
        private readonly IRepository repository;
        private readonly ICommit commit;
        private readonly ConcurrentDictionary<string, CommitFileLink> commitFilesByRepoRelativePath = new ConcurrentDictionary<string, CommitFileLink>(StringComparer.OrdinalIgnoreCase);

        public ElasticSearchCodexRepositoryStore(ElasticSearchStore store, IRepository repository, ICommit commit)
        {
            this.store = store;
            this.repository = repository;
            this.commit = commit;
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
                await batch.AddAsync(store.ProjectStore, new ProjectSearchModel() { Project = project });

                foreach (var projectReference in project.ProjectReferences)
                {
                    batch.Add(store.ProjectReferenceStore, new ProjectReferenceSearchModel(project)
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
                var storedFile = new TextSourceSearchModel()
                {
                    // TODO: This should probably be handled by custom serializer
                    File = file.EnableFullTextSearch()
                };

                await batch.AddAsync(store.TextSourceStore, storedFile);
                AddProperties(storedFile, file.Info.Properties);
            }
        }

        private void AddDefinitions(IEnumerable<DefinitionSymbol> definitions, bool declared)
        {
            Placeholder.Todo("Add declared/reference definitions to separate stored filters");
            foreach (var definition in definitions)
            {
                batch.Add(store.DefinitionStore, new DefinitionSearchModel()
                {
                    Definition = definition,
                });
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
                batch.Add(store.PropertyStore, new PropertySearchModel()
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

            batch.Add(store.TextSourceStore, textModel);

            var boundSourceModel = new BoundSourceSearchModel()
            {
                BindingInfo = boundSourceFile,
                TextUid = textModel.Uid,
                CompressedClassifications = new ClassificationListModel(boundSourceFile.Classifications)
            };

            await batch.AddAsync(store.BoundSourceStore, boundSourceModel, onAdded: _ =>
            {
                AddBoundSourceFileAssociatedData(boundSourceFile, boundSourceModel);
            });
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
                    Placeholder.Todo("Verify that this does not store the extra fields on IReferenceSpan and just the Symbol span fields");
                    referenceModel.Spans = spanList;
                }
                else
                {
                    referenceModel.CompressedSpans = new SymbolLineSpanListModel(spanList);
                }

                batch.Add(store.ReferenceStore, referenceModel);
            }
        }

        public async Task FinalizeAsync()
        {
            await batch.FlushAsync();
        }
    }
}