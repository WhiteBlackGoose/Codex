using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types.Api
{
    public class IndexCodex : ICodex
    {
        private Index Index;

        public Task<IIndexQueryResult<IReferenceSearchModel>> FindAllReferencesAsync(IReferenceSpan definition)
        {
            throw new NotImplementedException();
        }

        public Task<IIndexQueryResult<IDefinitionSearchModel>> FindDefinitionAsync(IReferenceSpan reference)
        {
            var query = Index.CreateQuery<IDefinitionSearchModel>();
            IDefinitionSearchModel modelTerms = null;

            var filter = modelTerms.Definition.SymbolId.AsTerm<IDefinitionSearchModel>().Equals<SymbolId>(reference.Symbol.SymbolId) |
            modelTerms.Definition.ProjectId.AsTerm<IDefinitionSearchModel>().Equals<string>(reference.Symbol.ProjectId);

            query.Filter = filter;
            query.MaxResults = 1000;

            return query.ExecuteAsync();
        }

        public Task<IIndexQueryResult<IReferenceSearchModel>> FindDefinitionLocationAsync(IReferenceSpan reference)
        {
            throw new NotImplementedException();
        }

        public Task<IIndexQueryResult<ISourceSearchModel>> GetSourceAsync(IReferenceSpan reference)
        {
            throw new NotImplementedException();
        }

        public Task<IIndexQueryResult<ISearchResult>> SearchAsync(string searchString)
        {
            throw new NotImplementedException();
        }
    }

    
}
