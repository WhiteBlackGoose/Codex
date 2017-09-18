using Codex.Analysis;
using Codex.Framework.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Utilities;

namespace Codex.ElasticSearch
{
    class ElasticSearchCodexRepositoryStore : ICodexRepositoryStore
    {
        private readonly ElasticSearchStore store;
        private readonly IRepository repository;
        private readonly ICommit commit;

        public ElasticSearchCodexRepositoryStore(ElasticSearchStore store, IRepository repository, ICommit commit)
        {
            this.store = store;
            this.repository = repository;
            this.commit = commit;
        }

        public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
        {
            throw new NotImplementedException();
        }

        public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> files)
        {
            throw new NotImplementedException();
        }

        public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> files)
        {
            throw new NotImplementedException();
        }

        public Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> files)
        {
            throw new NotImplementedException();
        }

        public async Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
        {
            await store.TextSourceStore.StoreAsync(files.Select(file => new TextSourceSearchModel()
            {
                ContentId = Placeholder.Value<string>("How to compute this?"),
                Uid = Placeholder.Value<string>("How to compute this?"),
                File = file
            }).AsReadOnlyList());
        }

        public Task FinalizeAsync()
        {
            throw new NotImplementedException();
        }
    }
}