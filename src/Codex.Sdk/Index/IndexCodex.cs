using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types.Api
{
    //public class IndexCodex : ICodex
    //{
    //    private Index Index;

    //    private ReferenceIndexDescriptor ReferenceIndexDescriptor;

    //    public Task<IIndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync(FindAllReferencesArguments arguments)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<IIndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments)
    //    {
    //        var query = Index.CreateQuery<IDefinitionSearchModel>();
    //        //IDefinitionSearchModel modelTerms = null;

    //        var filter = 
    //            ReferenceIndexDescriptor.ReferencedSymbol.ProjectId.Match(arguments.ProjectId) &
    //            ReferenceIndexDescriptor.ReferencedSymbol.SymbolId.Match(arguments.SymbolId);
    //        //var filter = modelTerms.Definition.SymbolId.AsTerm<IDefinitionSearchModel>().Equals<SymbolId>(reference.Symbol.SymbolId) |
    //        //modelTerms.Definition.ProjectId.AsTerm<IDefinitionSearchModel>().Equals<string>(reference.Symbol.ProjectId);

    //        //query.Filter = filter;
    //        //query.MaxResults = 1000;

    //        //return query.ExecuteAsync();
    //        throw new NotImplementedException();
    //    }

    //    public Task<IIndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments)
    //    {
    //        var query = Index.CreateQuery<IReferenceSearchModel>();

    //        throw new NotImplementedException();
    //    }

    //    public Task<IIndexQueryHitsResponse<ISourceSearchModel>> GetSourceAsync(GetSourceArguments arguments)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public Task<IIndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    
}
