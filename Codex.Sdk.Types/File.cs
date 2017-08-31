using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IBoundSourceFile : ITextFile, IFileScopeEntity
    {
        IReadOnlyList<IReferenceSpan> References { get; }

        // TODO: Should this be just the symbol
        IReadOnlyList<IDefinitionSpan> Definitions { get; }

        IReadOnlyList<IClassificationSpan> Classifications { get; }

        IReadOnlyList<IOutliningRegion> OutliningRegions { get; }
    }

    public interface ITextFile
    {
        [SearchBehavior(SearchBehavior.FullText)]
        string Content { get; }

        /// <summary>
        /// The number of lines in the file
        /// </summary>
        int LineCount { get; }

        /// <summary>
        /// The size of the file in bytes
        /// </summary>
        int Size { get; }

        /// <summary>
        /// The language of the file
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The relative path to the source file in the repository
        /// </summary>
        string RepoRelativePath { get; }
    }

    public interface IOutliningRegion
    {
        string Kind { get; }

        /// <summary>
        /// Defines the region containing the header text of the outlining region
        /// </summary>
        ILineSpan Header { get; }

        /// <summary>
        /// Defines the region containing the collapsible content region of the outlining region
        /// </summary>
        ILineSpan Content { get; }
    }

    public interface IDefinitionSpan : ISymbolSpan<IDefinitionSymbol>
    {
    }

    public interface IReferenceSpan : ISymbolSpan<IReferenceSymbol>
    {
        /// <summary>
        /// Gets the symbol id of the definition which provides this reference 
        /// (i.e. method definition for interface implementation)
        /// </summary>
        SymbolId RelatedDefinition { get; }
    }

    public interface IClassificationSpan : ISpan
    {
        int DefaultClassificationColor { get; }

        string Classification { get; }

        int LocalGroupId { get; }
    }

    public interface ISymbolSpan : ISpan
    {

    }

    public interface ILineSpan : ISpan
    {
        int LineNumber { get; }

        int LineSpanStart { get; }
    }

    public interface ISpan
    {
        int Start { get; }

        int Length { get; }
    }
}
