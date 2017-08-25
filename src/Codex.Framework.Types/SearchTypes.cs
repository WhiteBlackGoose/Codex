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
            .CopyTo(ds => ds.Language, ds => ds.Keywords)
            .CopyTo(ds => ds.ProjectId, ds => ds.Keywords);

        public SearchType ReferenceSearch = SearchType.Create<IReferenceSearchModel>()
            .CopyTo(rs => rs.References.First().Symbol.Kind, rs => rs.Symbol.Kind)
            .CopyTo(rs => rs.References.First().Symbol, rs => rs.Symbol);
    }

    public interface IDefinitionSearchModel : IFileScopeEntity
    {
        [Inline]
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
        [Inline]
        IReferenceSymbol Symbol { get; }

        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IReferenceSpan> References { get; }
    }

    public interface ISourceSearchModel : IFileScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        [Inline]
        IBoundSourceFile File { get; }
    }

    public interface IRepositorySearchModel : ISearchEntity
    {
        [Inline]
        IRepository Repository { get; }
    }

    public interface IProjectSearchModel : IProjectScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        [Inline]
        IProject Project { get; }
    }

    public interface IProjectReferenceSearchModel : IProjectScopeEntity, ICommitScopeEntity, ISearchEntity
    {
        [Inline]
        IProjectReference ProjectReference { get; }
    }
}

