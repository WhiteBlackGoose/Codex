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
using Lucene.Net.Index;
using Lucene.Net.Documents;
using Codex.ElasticSearch;
using Codex.Logging;

namespace Codex.Lucene.Search
{
    public class LuceneCodexStore : ICodexStore
    {
        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
            throw new NotImplementedException();
        }

        private class RepositoryStore : IndexingCodexRepositoryStoreBase<LuceneStoreFilterBuilder>
        {
            public RepositoryStore(IBatcher<LuceneStoreFilterBuilder> batcher, Logger logger, Repository repository, Commit commit, Branch branch) 
                : base(batcher, logger, repository, commit, branch)
            {
            }
        }

        private class LuceneStoreFilterBuilder
        {

        }
    }
}
