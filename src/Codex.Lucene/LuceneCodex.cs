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
    public class LuceneCodex : ICodex
    {
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
