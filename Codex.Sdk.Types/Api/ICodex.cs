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

        Task<IndexQueryResponse<IBoundSourceSearchModel>> GetSourceAsync(GetSourceArguments arguments);
    }

    public enum CodexServiceMethod
    {
        Search,
        FindAllRefs,
        FindDef,
        FindDefLocation,
        GetSource
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

    public interface ITextLineSpanResult : IProjectFileScopeEntity
    {
        ITextLineSpan TextSpan { get; }
    }

    public interface ISearchResult
    {
        /// <summary>
        /// The text span for a text result
        /// </summary>
        ITextLineSpanResult TextLine { get; }

        /// <summary>
        /// The definition of the search result
        /// </summary>
        IDefinitionSymbol Definition { get; }
    }

    public struct SerializableTimeSpan
    {
        public long Ticks { get; set; }

        public SerializableTimeSpan(TimeSpan timespan)
        {
            Ticks = timespan.Ticks;
        }

        public TimeSpan AsTimeSpan()
        {
            return TimeSpan.FromTicks(Ticks);
        }

        public static implicit operator TimeSpan(SerializableTimeSpan value)
        {
            return value.AsTimeSpan();
        }

        public static implicit operator SerializableTimeSpan(TimeSpan value)
        {
            return new SerializableTimeSpan(value);
        }

        public override string ToString()
        {
            return AsTimeSpan().ToString();
        }
    }

    public class IndexQueryResponse
    {
        /// <summary>
        /// If the query failed, this will contain the error message
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// The raw query sent to the index server
        /// </summary>
        public List<string> RawQueries { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public SerializableTimeSpan Duration { get; set; }

        /// <summary>
        /// The spent executing the query
        /// </summary>
        public SerializableTimeSpan ServerTime { get; set; }

        public override string ToString()
        {
            return $"Error: {Error}, Duration: {Duration}";
        }
    }

    public class IndexQueryResponse<T> : IndexQueryResponse
    {
        /// <summary>
        /// The results of the query
        /// </summary>
        public T Result { get; set; }

        public override string ToString()
        {
            return $"Result: {Result}, {base.ToString()}";
        }
    }

    public class IndexQueryHits<T>
    {
        /// <summary>
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of hits returned.
        /// </summary>
        public long Total { get; set; }

        /// <summary>
        /// The results of the query
        /// </summary>
        public List<T> Hits { get; set; }

        public override string ToString()
        {
            return $"Total: {Total}, {base.ToString()}";
        }
    }

    public class IndexQueryHitsResponse<T> : IndexQueryResponse<IndexQueryHits<T>>
    {
    }
}
