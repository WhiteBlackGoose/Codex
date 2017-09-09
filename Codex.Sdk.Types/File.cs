using Codex.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Represents a source file with associated semantic bindings
    /// </summary>
    public interface IBoundSourceFile : IProjectScopeEntity
    {
        /// <summary>
        /// The unique identifier for the file
        /// NOTE: This is not applicable to most files. Only set for files
        /// which are added in a shared context and need this for deduplication)
        /// </summary>
        string Uid { get; }

        /// <summary>
        /// The source file
        /// </summary>
        ISourceFile SourceFile { get; }

        /// <summary>
        /// The number of references in the file
        /// </summary>
        [CoerceGet(typeof(int?))]
        int ReferenceCount { get; }

        /// <summary>
        /// The number of definitions in the file
        /// </summary>
        [CoerceGet(typeof(int?))]
        int DefinitionCount { get; }

        /// <summary>
        /// References for the document. Sorted. May overlap.
        /// </summary>
        [ReadOnlyList]
        IReadOnlyList<IReferenceSpan> References { get; }

        // TODO: Should this be just the symbol
        /// <summary>
        /// Definitions for the document. Sorted. No overlap?
        /// </summary>
        IReadOnlyList<IDefinitionSpan> Definitions { get; }

        /// <summary>
        /// Classifications for the document. Sorted by start index. No overlap.
        /// </summary>
        [ReadOnlyList]
        IReadOnlyList<IClassificationSpan> Classifications { get; }

        /// <summary>
        /// Outlining regions for the document. May overlap.
        /// </summary>
        IReadOnlyList<IOutliningRegion> OutliningRegions { get; }

        /// <summary>
        /// Indicates that the file should be excluded from text search
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool ExcludeFromSearch { get; }
    }

    public interface ISourceFileInfo
    {
        /// <summary>
        /// The path to the source file in the project
        /// </summary>
        string Path { get; }

        /// <summary>
        /// The number of lines in the file
        /// </summary>
        int Lines { get; }

        /// <summary>
        /// The size of the file in bytes
        /// </summary>
        int Size { get; }

        /// <summary>
        /// The language of the file
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The web address of the file. TODO: Remove? Is repo relative path enough?
        /// </summary>
        string WebAddress { get; }

        /// <summary>
        /// The relative path to the source file in the repository
        /// </summary>
        string RepoRelativePath { get; }
    }

    /// <summary>
    /// Defines text contents of a file and associated data
    /// </summary>
    public interface ISourceFile : IFileScopeEntity
    {
        /// <summary>
        /// The information about the source file
        /// </summary>
        ISourceFileInfo Info { get; }

        /// <summary>
        /// The encoding used for the file
        /// </summary>
        string Encoding { get; }

        /// <summary>
        /// The content of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.FullText)]
        string Content { get; }
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

    public interface IDefinitionSpan : ISymbolSpan
    {
        /// <summary>
        /// The definition symbol referred to by the span
        /// </summary>
        IDefinitionSymbol Definition { get; }
    }

    public interface IReferenceSpan : ISymbolSpan
    {
        /// <summary>
        /// Gets the symbol id of the definition which provides this reference 
        /// (i.e. method definition for interface implementation)
        /// </summary>
        SymbolId RelatedDefinition { get; }

        /// <summary>
        /// The reference symbol referred to by the span
        /// </summary>
        IReferenceSymbol Reference { get; }
    }

    /// <summary>
    /// Defines a classified span of text
    /// </summary>
    public interface IClassificationSpan : ISpan
    {
        /// <summary>
        /// The default classification color for the span. This is used for
        /// contexts where a mapping from classification id to color is not
        /// available.
        /// </summary>
        int DefaultClassificationColor { get; }

        /// <summary>
        /// The classification identifier for the span
        /// </summary>
        string Classification { get; }

        /// <summary>
        /// The identifier to the local group of spans which refer to the same common symbol
        /// </summary>
        int LocalGroupId { get; }
    }

    public interface ISymbolSpan : ILineSpan
    {
        /// <summary>
        /// The line text
        /// </summary>
        string LineSpanText { get; }
    }

    public interface ILineSpan : ISpan
    {
        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        int LineNumber { get; }

        /// <summary>
        /// The character position where the span starts in the line text
        /// </summary>
        int LineSpanStart { get; }

        /// <summary>
        /// If positive, the offset of the line span from the beginning of the line
        /// If negative, the offset of the linespan from the end of the next line
        /// </summary>
        int LineOffset { get; }
    }

    public interface ISpan
    {
        /// <summary>
        /// The absolute character offset of the span within the document
        /// </summary>
        int Start { get; }

        /// <summary>
        /// The length of the span
        /// </summary>
        int Length { get; }
    }
}
