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
            .CopyTo(ds => ds.Definition.Kind, ds => ds.Keywords)
            //.CopyTo(ds => ds.Language, ds => ds.Keywords)
            .CopyTo(ds => ds.ProjectId, ds => ds.Keywords);

        public static SearchType Reference = SearchType.Create<IReferenceSearchModel>(RegisteredSearchTypes)
            .CopyTo(rs => rs.References.First().Symbol.Kind, rs => rs.ReferencedSymbol.Kind)
            .CopyTo(rs => rs.References.First().Symbol, rs => rs.ReferencedSymbol);


        public static SearchType Source = SearchType.Create<ISourceSearchModel>(RegisteredSearchTypes);

        public static SearchType Language = SearchType.Create<ILanguage>(RegisteredSearchTypes);

        public static SearchType Repository = SearchType.Create<IRepositorySearchModel>(RegisteredSearchTypes);

        public static SearchType Project = SearchType.Create<IProjectSearchModel>(RegisteredSearchTypes);

        public static SearchType ProjectReference = SearchType.Create<IProjectReferenceSearchModel>(RegisteredSearchTypes);
    }

    public class DefinitionIndexType : SearchType<IDefinitionSymbol>
    {
        public DefinitionIndexType(string name) : base(name)
        {
        }
    }

    public class IndexField<T>
    {
        public SearchBehavior SearchBehavior { get; }
    }


    public interface IDefinitionSearchModel : ISearchEntity
    {
        [SearchDescriptorInline(false)]
        IDefinitionSymbol Definition { get; }

        // TODO: Should this be here?
        [SearchBehavior(SearchBehavior.Prefix)]
        string PrefixTerms { get; }

        /// <summary>
        /// Keywords are additional terms which can be used to find a given symbol.
        /// NOTE: Keywords can only be used to select from symbols which have
        /// a primary term match
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Keywords { get; }
    }

    public interface ILanguage : ISearchEntity
    {
        string Name { get; }
    }

    public interface IReferenceSearchModel : IFileScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        [SearchDescriptorInline(false)]
        IReferenceSymbol ReferencedSymbol { get; }

        // TODO: Need some sort of override for searching RelatedDefinition of the
        // ReferenceSpan
        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IReferenceSpan> References { get; }
    }

    public interface ISourceSearchModel : IFileScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        IBoundSourceFile File { get; }
    }

    public interface IRepositorySearchModel : ISearchEntity
    {
        IRepository Repository { get; }
    }

    public interface IProjectSearchModel : IProjectScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        IProject Project { get; }
    }

    public interface IProjectReferenceSearchModel : IProjectScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        IProjectReference ProjectReference { get; }
    }
}

