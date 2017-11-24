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

        public static SearchType Definition = SearchType.Create<IDefinitionSearchModel>(RegisteredSearchTypes);
        //.CopyTo(ds => ds.Definition.Modifiers, ds => ds.Keywords)
        //.CopyTo(ds => ds.Definition.Kind, ds => ds.Kind)
        //.CopyTo(ds => ds.Definition.ExcludeFromDefaultSearch, ds => ds.ExcludeFromDefaultSearch)
        //.CopyTo(ds => ds.Definition.Kind, ds => ds.Keywords)
        //.CopyTo(ds => ds.Definition.ShortName, ds => ds.ShortName)
        ////.CopyTo(ds => ds.Language, ds => ds.Keywords)
        //.CopyTo(ds => ds.Definition.ProjectId, ds => ds.ProjectId)
        //.CopyTo(ds => ds.Definition.ProjectId, ds => ds.Keywords);

        public static SearchType Reference = SearchType.Create<IReferenceSearchModel>(RegisteredSearchTypes);
        //.CopyTo(rs => rs.Spans.First().Reference, rs => rs.Reference);

        public static SearchType TextSource = SearchType.Create<ITextSourceSearchModel>(RegisteredSearchTypes);
        //.CopyTo(ss => ss.File.SourceFile.Content, ss => ss.Content)
        //.CopyTo(ss => ss.File.SourceFile.Info.RepoRelativePath, ss => ss.RepoRelativePath)
        //.CopyTo(ss => ss.File.ProjectId, ss => ss.ProjectId)
        //.CopyTo(ss => ss.File.Info.Path, ss => ss.FilePath);

        public static SearchType BoundSource = SearchType.Create<IBoundSourceSearchModel>(RegisteredSearchTypes);
            //.CopyTo(ss => ss.File.SourceFile.Content, ss => ss.Content)
            //.CopyTo(ss => ss.File.SourceFile.Info.RepoRelativePath, ss => ss.RepoRelativePath)
            //.CopyTo(ss => ss.BindingInfo.ProjectId, ss => ss.ProjectId)
            //.CopyTo(ss => ss.FilePath, ss => ss.FilePath);

        public static SearchType Language = SearchType.Create<ILanguageSearchModel>(RegisteredSearchTypes);

        public static SearchType Repository = SearchType.Create<IRepositorySearchModel>(RegisteredSearchTypes);

        public static SearchType Project = SearchType.Create<IProjectSearchModel>(RegisteredSearchTypes)
            .Exclude(sm => sm.Project.ProjectReferences.First().Definitions);

        public static SearchType Commit = SearchType.Create<ICommitSearchModel>(RegisteredSearchTypes);

        public static SearchType CommitFiles = SearchType.Create<ICommitFilesSearchModel>(RegisteredSearchTypes);

        public static SearchType ProjectReference = SearchType.Create<IProjectReferenceSearchModel>(RegisteredSearchTypes);

        public static SearchType Property = SearchType.Create<IPropertySearchModel>(RegisteredSearchTypes);

        public static SearchType StoredFilter = SearchType.Create<IStoredFilter>(RegisteredSearchTypes);

        public static SearchType RegisteredEntity = SearchType.Create<IRegisteredEntity>(RegisteredSearchTypes);
    }

    /// <summary>
    /// Defines a stored filter which matches entities in a particular index shard in a stable manner
    /// </summary>
    public interface IRegisteredEntity : ISearchEntity
    {
        /// <summary>
        /// The date in which the entity was registered (i.e. added to the store)
        /// </summary>
        DateTime DateAdded { get; set; }
    }

    /// <summary>
    /// Defines a stored filter which matches entities in a particular index shard in a stable manner
    /// </summary>
    public interface IStoredFilter : ISearchEntity
    {
        /// <summary>
        /// The time of the last update to the stored filter
        /// </summary>
        DateTime DateUpdated { get; set; }

        /// <summary>
        /// The name of the index to which the stored filter applies
        /// </summary>
        string IndexName { get; }

        /// <summary>
        /// The shard to which the stored filter applies
        /// </summary>
        int Shard { get; }

        /// <summary>
        /// List of stable ids to include in the stored filter.
        /// </summary>
        IReadOnlyList<long> StableIds { get; }

        /// <summary>
        /// List of uids to for stored filters which will be unioned with the given stored filter
        /// </summary>
        IReadOnlyList<string> BaseUids { get; }

        /// <summary>
        /// The filter which matches entities in the index shard
        /// </summary>
        object Filter { get; }
    }

    public interface IDefinitionSearchModel : ISearchEntity
    {
        IDefinitionSymbol Definition { get; }

        // TODO: Not sure that this is actually needed
        ///// <summary>
        ///// Keywords are additional terms which can be used to find a given symbol.
        ///// NOTE: Keywords can only be used to select from symbols which have
        ///// a primary term match
        ///// </summary>
        //[SearchBehavior(SearchBehavior.NormalizedKeyword)]
        //IReadOnlyList<string> Keywords { get; }
    }

    public interface ILanguageSearchModel : ISearchEntity
    {
        ILanguageInfo Language { get; }
    }

    // TODO: Don't inherit reference symbol. Instead just have a member
    public interface IReferenceSearchModel : IProjectFileScopeEntity, ISearchEntity
    {
        /// <summary>
        /// The reference symbol
        /// </summary>
        ICodeSymbol Reference { get; }

        // TODO: Store efficient representation of list of reference spans (old implementation
        // has two possible representations).
        // There should probably be some placeholder interfaces in SDK for the efficient types which
        // the JSON serializer will be customized to replace with real type
        // TODO: Need some sort of override for searching RelatedDefinition of the
        // ReferenceSpan
        [SearchBehavior(SearchBehavior.None)]
        [ReadOnlyList]
        [CoerceGet]
        IReadOnlyList<ISymbolSpan> Spans { get; }

        /// <summary>
        /// Compressed list of spans
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        ISymbolLineSpanList CompressedSpans { get; }
    }

    public interface IBoundSourceSearchModel : ISearchEntity
    {
        /// <summary>
        /// The unique identifier of the associated <see cref="ISourceFile"/>
        /// </summary>
        string TextUid { get; }

        /// <summary>
        /// The binding info
        /// </summary>
        IBoundSourceInfo BindingInfo { get; }

        /// <summary>
        /// Compressed list of classification spans
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        IClassificationList CompressedClassifications { get; }

        /// <summary>
        /// Compressed list of reference spans
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        IReferenceList CompressedReferences { get; }
    }

    public interface ITextSourceSearchModel : ISearchEntity
    {
        ISourceFile File { get; }
    }

    public interface IRepositorySearchModel : ISearchEntity
    {
        IRepository Repository { get; }
    }

    public interface IProjectSearchModel : ISearchEntity
    {
        IProject Project { get; }
    }

    public interface IProjectReferenceSearchModel : IProjectScopeEntity, ISearchEntity
    {
        IReferencedProject ProjectReference { get; }
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

