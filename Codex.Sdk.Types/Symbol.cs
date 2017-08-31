using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface ISymbolSpan<TSymbol> : ILineSpan
        where TSymbol : ICodeSymbol
    {
        TSymbol Symbol { get; }
    }

    public interface IDefinitionSymbol : IReferenceSymbol
    {
        /// <summary>
        /// The unique identifier for the symbol
        /// NOTE: This is not applicable to most symbols. Only set for symbols
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        string Uid { get; set; }

        /// <summary>
        /// The comment applied to the definition
        /// </summary>
        string Comment { get; }

        /// <summary>
        /// The display name of the symbol
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The short name of the symbol (i.e. Task).
        /// This is used to find the symbol when search term does not contain '.'
        /// </summary>
        [SearchBehavior(SearchBehavior.Prefix)]
        string ShortName { get; }

        /// <summary>
        /// The qualified name of the symbol (i.e. System.Threading.Tasks.Task).
        /// This is used to find the symbol when the search term contains '.'
        /// </summary>
        [SearchBehavior(SearchBehavior.PrefixFullName)]
        string ContainerQualifiedName { get; }

        /// <summary>
        /// Modifiers for the symbol such as public
        /// </summary>
        string[] Modifiers { get; }
    }

    public interface IReferenceSymbol : ICodeSymbol
    {
        /// <summary>
        /// The kind of reference. This is used to group references.
        /// (i.e. for C#(aka .NET) MethodOverride, InterfaceMemberImplementation, InterfaceImplementation, etc.)
        /// </summary>
        string ReferenceKind { get; }
    }

    public interface ICodeSymbol
    {
        /// <summary>
        /// The identifier of the referenced project
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string ProjectId { get; }

        /// <summary>
        /// The identifier for the symbol
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        SymbolId SymbolId { get; }

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

        /// <summary>
        /// Indicates if the symbol should NEVER be included in the definition/find all references search.
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool ExcludeFromSearch { get; }

        /// <summary>
        /// Indicates the corresponding definitions is implicitly declared and therefore this should not be
        /// used for find all references search
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool IsImplicitlyDeclared { get; }
    }
}
