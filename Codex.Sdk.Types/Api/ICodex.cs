using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Sdk.Search
{
    // +TODO: Generate ASP.Net endpoint which handles all these calls. Potentially also implement
    // caller (i.e. WebApiCodex : ICodex)
    /// <summary>
    /// High level operations for codex 
    /// </summary>
    public interface ICodex
    {
        Task<IndexQueryHitsResponse<ISearchResult>> SearchAsync(SearchArguments arguments);

        Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindAllReferencesAsync(FindAllReferencesArguments arguments);

        /// <summary>
        /// Find definition for a symbol
        /// Usage: Documentation hover tooltip
        /// </summary>
        Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        Task<IndexQueryHitsResponse<IReferenceSearchModel>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments);

        Task<IndexQueryHitsResponse<IBoundSourceSearchModel>> GetSourceAsync(GetSourceArguments arguments);
    }

    public class CodexArgumentsBase
    {
        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults = 100;
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

    public interface ISearchResult : IProjectFileScopeEntity
    {
        /// <summary>
        /// The text span for a text result
        /// </summary>
        ITextLineSpan TextSpan { get; }
    }

    public class IndexQueryResponse<T>
    {
        /// <summary>
        /// If the query failed, this will contain the error message
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The raw query sent to the index server
        /// </summary>
        public IReadOnlyList<string> RawQueries { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public TimeSpan ServerTime { get; set; }

        /// <summary>
        /// The results of the query
        /// </summary>
        public T Result { get; set; }
    }

    public class IndexQueryHits<T>
    {
        /// <summary>
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of hits returned.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// The results of the query
        /// </summary>
        public IReadOnlyList<T> Hits { get; set; }
    }

    public class IndexQueryHitsResponse<T> : IndexQueryResponse<IndexQueryHits<T>>
    {
    }
}
