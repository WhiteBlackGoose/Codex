using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.ElasticSearch.Search
{
    public class StoredFilterSearchContext : ClientContext
    {
        public string RepositoryScopeId { get; }

        public string StoredFilterIndexName { get; }

        public string StoredFilterUid { get; }

        public StoredFilterSearchContext(ClientContext context, string repositoryScopeId, string storedFilterIndexName, string storedFilterUid)
            : base(context)
        {
            RepositoryScopeId = repositoryScopeId;
            StoredFilterIndexName = storedFilterIndexName;
            StoredFilterUid = storedFilterUid;
        }
    }
}
