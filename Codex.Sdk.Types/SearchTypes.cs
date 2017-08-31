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
    class SearchTypes
    {
        public SearchType DefinitionSearch = SearchType.Create<IDefinitionSearchModel>()
            .CopyTo(ds => ds.Definition.Modifiers, ds => ds.Keywords)
            .CopyTo(ds => ds.Definition.Kind, ds => ds.Keywords)
            //.CopyTo(ds => ds.Language, ds => ds.Keywords)
            .CopyTo(ds => ds.ProjectId, ds => ds.Keywords);

        public SearchType ReferenceSearch = SearchType.Create<IReferenceSearchModel>()
            .CopyTo(rs => rs.References.First().Symbol.Kind, rs => rs.ReferencedSymbol.Kind)
            .CopyTo(rs => rs.References.First().Symbol, rs => rs.ReferencedSymbol);
    }

    public interface IDefinitionSearchModel : IFileScopeEntity, ISearchEntity
    {
        [Inline(false)]
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
        [Inline(false)]
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

