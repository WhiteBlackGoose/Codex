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

namespace Codex.Lucene.Search
{
    public class LuceneCodexStore : ICodexStore
    {
        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
            throw new NotImplementedException();
        }

        private class RepositoryStore : ICodexRepositoryStore
        {
            public Task AddBoundFilesAsync(IReadOnlyList<BoundSourceFile> files)
            {
                throw new NotImplementedException();
            }

            public Task AddCommitFilesAsync(IReadOnlyList<CommitFileLink> links)
            {
                throw new NotImplementedException();
            }

            public Task AddLanguagesAsync(IReadOnlyList<LanguageInfo> languages)
            {
                throw new NotImplementedException();
            }

            public Task AddProjectsAsync(IReadOnlyList<AnalyzedProject> projects)
            {
                throw new NotImplementedException();
            }

            public Task AddTextFilesAsync(IReadOnlyList<SourceFile> files)
            {
                throw new NotImplementedException();
            }

            public Task FinalizeAsync()
            {
                throw new NotImplementedException();
            }
        }
    }
}
