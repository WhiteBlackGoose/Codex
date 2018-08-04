using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Analysis;
using Codex.ObjectModel;
using Codex.Storage;
using Codex.Utilities;

namespace Codex.ElasticSearch.Legacy.Bridge
{
    /// <summary>
    /// ICodexStore implementation using legacy ElasticsearchStorage
    /// </summary>
    public class LegacyElasticSearchStore : ICodexStore
    {
        private ElasticsearchStorage Storage { get; }
        private LegacyElasticSearchStoreConfiguration Configuration { get; }

        public LegacyElasticSearchStore(LegacyElasticSearchStoreConfiguration configuration)
        {
            Configuration = configuration;
            Storage = new ElasticsearchStorage(configuration.Endpoint);
        }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
            repository.Name = repository.Name ?? commit.RepositoryName;
            var repositoryStore = new RepositoryStore(this, repository);
            return repositoryStore.InitializeAsync();
        }

        private class RepositoryStore : ICodexRepositoryStore
        {
            private readonly LegacyElasticSearchStore store;
            private readonly Repository repository;
            private ElasticsearchStorage Storage => store.Storage;
            private readonly string targetIndex;

            public RepositoryStore(LegacyElasticSearchStore store, Repository repository)
            {
                this.store = store;
                this.repository = repository;
                this.targetIndex = store.Configuration.TargetIndexName ?? StoreUtilities.GetTargetIndexName(repository.Name);
            }

            public async Task<ICodexRepositoryStore> InitializeAsync()
            {
                await Storage.AddRepository(targetIndex: targetIndex, repositoryName: repository.Name);
                return this;
            }

            public async Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
            {
                foreach (var file in files)
                {
                    if (repository.SourceControlWebAddress != null && file.RepoRelativePath != null)
                    {
                        file.SourceFile.Info.WebAddress = StoreUtilities.GetFileWebAddress(repository.SourceControlWebAddress, file.RepoRelativePath);
                    }

                    await Storage.UploadAsync(targetIndex, file);
                }
            }

            public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
            {
                return Task.CompletedTask;
            }

            public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
            {
                throw new NotImplementedException();
            }

            public async Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
            {
                foreach (var project in projects)
                {
                    await Storage.AddProjectAsync(project, targetIndex);

                    await AddBoundFilesAsync(project.AdditionalSourceFiles);
                }
            }

            public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
            {
                throw new NotImplementedException();
            }

            public Task FinalizeAsync()
            {
                return Storage.FinalizeRepository(targetIndex: targetIndex, repositoryName: repository.Name);
            }
        }
    }
}
