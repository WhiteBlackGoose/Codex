using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    /// <summary>
    /// High level operations for codex 
    /// </summary>
    public interface ICodex
    {
        Task<IIndexQueryResult<ISearchResult>> SearchAsync(string searchString);

        Task<IIndexQueryResult<IReferenceSearchModel>> FindAllReferencesAsync(IReferenceSpan definition);

        /// <summary>
        /// Find definition for a symbol
        /// Usage: Documentation hover tooltip
        /// </summary>
        Task<IIndexQueryResult<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        Task<IIndexQueryResult<IReferenceSearchModel>> FindDefinitionLocationAsync(IReferenceSpan reference);

        Task<IIndexQueryResult<ISourceSearchModel>> GetSourceAsync(IReferenceSpan reference);
    }

    public class FindDefinitionArguments
    {
        public string SymbolId;
        public string ProjectId;
    }

    public class FindAllReferencesArguments
    {
        public string SymbolId;
        public string ProjectId;
    }

    public interface ISearchResult
    {

    }
}
