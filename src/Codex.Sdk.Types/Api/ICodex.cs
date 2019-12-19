﻿using System;
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

        Task<IndexQueryResponse<ReferencesResult>> FindAllReferencesAsync(FindAllReferencesArguments arguments);

        /// <summary>
        /// Find definition for a symbol
        /// Usage: Documentation hover tooltip
        /// </summary>
        Task<IndexQueryHitsResponse<IDefinitionSearchModel>> FindDefinitionAsync(FindDefinitionArguments arguments);

        /// <summary>
        /// Find definition location for a symbol
        /// Usage: Go To Definition
        /// </summary>
        Task<IndexQueryResponse<ReferencesResult>> FindDefinitionLocationAsync(FindDefinitionLocationArguments arguments);

        Task<IndexQueryResponse<IBoundSourceFile>> GetSourceAsync(GetSourceArguments arguments);

        Task<IndexQueryResponse<GetProjectResult>> GetProjectAsync(GetProjectArguments arguments);
    }

    public static class CodexSearchExtensions
    {
        public static async Task<string> GetFirstDefinitionFilePath(this ICodex codex, string projectId, string symbolId)
        {
            var response = await codex.FindDefinitionLocationAsync(new FindDefinitionLocationArguments()
            {
                ProjectId = projectId,
                SymbolId = symbolId,
                FallbackFindAllReferences = false,
                MaxResults = 1
            });

            return (response.Error != null || response.Result.Total == 0) ? null : response.Result.Hits[0].ProjectRelativePath;
        }
    }

    public enum CodexServiceMethod
    {
        Search,
        FindAllRefs,
        FindDef,
        FindDefLocation,
        GetSource,
        GetProject,
    }

    public class CodexArgumentsBase
    {
        /// <summary>
        /// The maximum number of results to return
        /// </summary>
        public int MaxResults { get; set; } = 100;
    }

    public class ContextCodexArgumentsBase : CodexArgumentsBase
    {
        public const string AllRepositoryScopeId = "_all";

        /// <summary>
        /// The id of the repository to which to scope search results
        /// </summary>
        public string RepositoryScopeId { get; set; }

        /// <summary>
        /// The id of the project to which to scope search results
        /// </summary>
        public string ProjectScopeId { get; set; }

        /// <summary>
        /// The id of the repository referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingRepositoryId { get; set; }

        /// <summary>
        /// The id of the project referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingProjectId { get; set; }

        /// <summary>
        /// The id of the file referencing the symbol.
        /// NOTE: This is used to priority inter-repository matches over
        /// matches from outside the repository
        /// </summary>
        public string ReferencingFileId { get; set; }
    }

    public class FindSymbolArgumentsBase : ContextCodexArgumentsBase
    {
        /// <summary>
        /// The symbol id of the symbol
        /// </summary>
        public string SymbolId { get; set; }

        /// <summary>
        /// The project id of the symbol
        /// </summary>
        public string ProjectId { get; set; }
    }

    public class FindDefinitionArguments : FindSymbolArgumentsBase
    {

    }

    public class FindAllReferencesArguments : FindSymbolArgumentsBase
    {
        public string ReferenceKind { get; set; }
    }

    public class FindDefinitionLocationArguments : FindAllReferencesArguments
    {
        public bool FallbackFindAllReferences { get; set; } = true;
    }

    public class SearchArguments : ContextCodexArgumentsBase
    {
        public string SearchString { get; set; }

        public bool AllowReferencedDefinitions { get; set; } = false;

        public bool FallbackToTextSearch { get; set; } = true;

        public bool TextSearch { get; set; } = false;
    }

    public class GetProjectArguments : ContextCodexArgumentsBase
    {
        public string ProjectId { get; set; }
    }

    public class GetProjectResult
    {
        public DateTime DateUploaded { get; set; }

        public IProject Project { get; set; }

        public List<string> ReferencingProjects { get; set; } = new List<string>();
    }

    public class GetSourceArguments : ContextCodexArgumentsBase
    {
        // TODO: Add argument for getting just text content

        public string ProjectId { get; set; }

        public string ProjectRelativePath { get; set; }

        public bool IncludeWebAddress { get; set; }

        public bool DefinitionOutline { get; set; } = false;
    }

    public interface IReferenceSearchResult : IProjectFileScopeEntity
    {
        IReferenceSpan ReferenceSpan { get; }
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
        private long total;

        /// <summary>
        /// The total number of results matching the query. 
        /// NOTE: This may be greater than the number of hits returned.
        /// </summary>
        public long Total
        {
            get => total == 0 ? Hits?.Count ?? 0 : total;
            set
            {
                total = value;
            }
        }

        /// <summary>
        /// The results of the query
        /// </summary>
        public List<T> Hits { get; set; } = new List<T>(0);

        public override string ToString()
        {
            return $"Total: {Total}, {base.ToString()}";
        }
    }

    public class ReferencesResult : IndexQueryHits<IReferenceSearchResult>
    {
        public string SymbolDisplayName { get; set; }

        public string SymbolId { get; set; }

        public string ProjectId { get; set; }

        public List<RelatedDefinition> RelatedDefinitions { get; } = new List<RelatedDefinition>();
    }

    public class RelatedDefinition
    {
        public string ReferenceKind { get; }
        public IDefinitionSymbol Symbol { get; }

        public RelatedDefinition(IDefinitionSymbol symbol, string referenceKind)
        {
            Symbol = symbol;
            ReferenceKind = referenceKind;
        }
    }

    public class IndexQueryHitsResponse<T> : IndexQueryResponse<IndexQueryHits<T>>
    {
    }
}
