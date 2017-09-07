using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /*
     * Types in this file define search behaviors. Changes should be made with caution as they can affect
     * the mapping schema for indices and will generally need to be backward compatible.
     * Additions should be generally safe.
     */
    public class SearchTypes
    {
        public static readonly List<SearchType> RegisteredSearchTypes = new List<SearchType>();

        public static SearchType Definition = SearchType.Create<IDefinitionSearchModel>(RegisteredSearchTypes)
            .CopyTo(ds => ds.Definition.Modifiers, ds => ds.Keywords)
            .CopyTo(ds => ds.Definition.Kind, ds => ds.Kind)
            .CopyTo(ds => ds.Definition.ExcludeFromDefaultSearch, ds => ds.ExcludeFromDefaultSearch)
            .CopyTo(ds => ds.Definition.Kind, ds => ds.Keywords)
            .CopyTo(ds => ds.Definition.ShortName, ds => ds.ShortName)
            //.CopyTo(ds => ds.Language, ds => ds.Keywords)
            .CopyTo(ds => ds.Definition.ProjectId, ds => ds.ProjectId)
            .CopyTo(ds => ds.Definition.ProjectId, ds => ds.Keywords);

        public static SearchType Reference = SearchType.Create<IReferenceSearchModel>(RegisteredSearchTypes)
            .Inherit<IReferenceSymbol>(rs => rs.References.First().Symbol, rs => rs);

        public static SearchType Source = SearchType.Create<ISourceSearchModel>(RegisteredSearchTypes)
            .CopyTo(ss => ss.File.Content, ss => ss.Content)
            .CopyTo(ss => ss.File.RepoRelativePath, ss => ss.RepoRelativePath)
            .CopyTo(ss => ss.File.ProjectId, ss => ss.ProjectId)
            .CopyTo(ss => ss.File.FilePath, ss => ss.FilePath);

        public static SearchType Language = SearchType.Create<ILanguageSearchModel>(RegisteredSearchTypes);

        public static SearchType Repository = SearchType.Create<IRepositorySearchModel>(RegisteredSearchTypes);

        public static SearchType Project = SearchType.Create<IProjectSearchModel>(RegisteredSearchTypes);

        public static SearchType Commit = SearchType.Create<ICommitSearchModel>(RegisteredSearchTypes);

        public static SearchType CommitFiles = SearchType.Create<ICommitFilesSearchModel>(RegisteredSearchTypes);

        public static SearchType ProjectReference = SearchType.Create<IProjectReferenceSearchModel>(RegisteredSearchTypes);

        public static SearchType Property = SearchType.Create<IPropertySearchModel>(RegisteredSearchTypes);
    }

    public interface IDefinitionSearchModel : ISearchEntity
    {
        IDefinitionSymbol Definition { get; }

        /// <summary>
        /// The identifier of the project in which the symbol appears
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string ProjectId { get; }

        /// <summary>
        /// The identifier for the symbol
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string SymbolId { get; }

        /// <summary>
        /// The symbol kind. (i.e. interface, method, field)
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Kind { get; }

        /// <summary>
        /// Indicates if the symbol should be excluded from the definition/find all references search (by default).
        /// Symbol will only be included if kind is explicitly specified
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool ExcludeFromDefaultSearch { get; }

        [SearchBehavior(SearchBehavior.Prefix)]
        string ShortName { get; }

        [SearchBehavior(SearchBehavior.PrefixFullName)]
        string ContainerQualifiedName { get; }

        /// <summary>
        /// Keywords are additional terms which can be used to find a given symbol.
        /// NOTE: Keywords can only be used to select from symbols which have
        /// a primary term match
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        IReadOnlyList<string> Keywords { get; }
    }

    public interface ILanguageSearchModel : ISearchEntity
    {
        string Name { get; }
    }

    public interface IReferenceSearchModel : IReferenceSymbol, IFileScopeEntity, ISearchEntity
    {
        // TODO: Need some sort of override for searching RelatedDefinition of the
        // ReferenceSpan
        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IReferenceSpan> References { get; }
    }

    public interface ISourceSearchModel : IFileScopeEntity, ISearchEntity
    {
        /// <summary>
        /// The content of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.FullText)]
        string Content { get; }

        /// <summary>
        /// The language of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.FullText)]
        string Language { get; }

        /// <summary>
        /// The relative path to the source file in the repository
        /// </summary>
        [SearchBehavior(SearchBehavior.HierarchicalPath)]
        string RepoRelativePath { get; }

        IBoundSourceFile File { get; }
    }

    public interface IRepositorySearchModel : IRepoScopeEntity, ISearchEntity
    {
        IRepository Repository { get; }
    }

    public interface IProjectSearchModel : IProjectScopeEntity, ISearchEntity
    {
        IProject Project { get; }
    }

    public interface IProjectReferenceSearchModel : IProjectScopeEntity, ISearchEntity
    {
        IProjectReference ProjectReference { get; }
    }

    public interface ICommitSearchModel : ISearchEntity
    {
        ICommit Commit { get; }
    }

    /// <summary>
    /// The set of files present in the repository at a given commit.
    /// </summary>
    public interface ICommitFilesSearchModel : ICommitScopeEntity, IRepoScopeEntity, ISearchEntity
    {
        IReadOnlyList<ICommitFileLink> CommitFiles { get; }
    }
}

