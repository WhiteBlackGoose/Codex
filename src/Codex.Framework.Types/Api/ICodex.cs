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
        Task<IIndexQueryResult<IDefinitionSearchModel>> FindDefinitionAsync(IReferenceSpan reference);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        Task<IIndexQueryResult<IReferenceSearchModel>> FindDefinitionLocationAsync(IReferenceSpan reference);

        Task<IIndexQueryResult<ISourceSearchModel>> GetSourceAsync(IReferenceSpan reference);
    }

    public interface ISearchResult
    {

    }
}
