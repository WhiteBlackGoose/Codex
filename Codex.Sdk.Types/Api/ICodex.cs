using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    // TODO: Generate ASP.Net endpoint which handles all these calls. Potentially also implement
    // caller (i.e. WebApiCodex : ICodex)
    /// <summary>
    /// High level operations for codex 
    /// </summary>
    public interface ICodexService
    {
        Task<IIndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments);

        Task<IIndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync(FindAllReferencesArguments arguments);

        /// <summary>
        /// Find definition for a symbol
        /// Usage: Documentation hover tooltip
        /// </summary>
        Task<IIndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        Task<IIndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments);

        Task<IIndexQueryHitsResponse<ISourceSearchModel>> GetSourceAsync(GetSourceArguments arguments);
    }

    public class CodexArgumentsBase
    {
        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults;
    }

    public class ContextCodexArgumentsBase : CodexArgumentsBase
    {
        /// <summary>
        /// The id of the repository referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingRepositoryId;

        /// <summary>
        /// The id of the project referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingProjectId;

        /// <summary>
        /// The id of the file referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingFileId;
    }

    public class FindSymbolArgumentsBase : ContextCodexArgumentsBase
    {
        /// <summary>
        /// The symbol id of the symbol
        /// </summary>
        public string SymbolId;

        /// <summary>
        /// The project id of the symbol
        /// </summary>
        public string ProjectId;
    }

    public class FindDefinitionArguments : FindSymbolArgumentsBase
    {

    }

    public class FindAllReferencesArguments : FindSymbolArgumentsBase
    {
    }

    public class FindDefinitionLocationArguments : FindSymbolArgumentsBase
    {
    }

    public class SearchArguments : ContextCodexArgumentsBase
    {
        public string SearchString;
    }

    public class GetSourceArguments : ContextCodexArgumentsBase
    {

    }

    public interface ISearchResult
    {

    }
}
