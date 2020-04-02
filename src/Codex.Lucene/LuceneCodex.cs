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
using Lucene.Net.Store;
using Lucene.Net.Search;

namespace Codex.Lucene.Search
{
    public class LuceneCodex : ICodex
    {
        private LuceneConfiguration Configuration { get; }
        private IndexReader Reader { get; }
        private IndexSearcher Searcher { get; }

        public LuceneCodex(LuceneConfiguration configuration)
        {
            Configuration = configuration;
            Reader = DirectoryReader.Open(FSDirectory.Open(configuration.Directory));
            Searcher = new IndexSearcher(Reader);
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments)
        {
            throw new NotImplementedException();
        }

        public Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
        {
            throw new NotImplementedException();
        }
    }
}
