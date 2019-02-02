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
    public partial interface IBoundSourceFile : IBoundSourceInfo
    {
        /// <summary>
        /// The source file
        /// </summary>
        ISourceFile SourceFile { get; }

        /// <summary>
        /// Gets the commit referencing the file.
        /// </summary>
        ICommit Commit { get; }

        /// <summary>
        /// Gets the repository containing the file.
        /// </summary>
        IRepository Repo { get; }

        /// <summary>
        /// The lines in the source file
        /// </summary>
        //[Include(ObjectStage.Analysis)]
        //IReadOnlyList<string> SourceFileContentLines { get; }
    }

    public interface IBoundSourceInfo
    {
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
        /// The language of the file
        /// TODO: Remove
        /// </summary>
        string Language { get; }

        /// <summary>
        /// References for the document. Sorted. May overlap.
        /// </summary>
        [ReadOnlyList]
        [SearchBehavior(SearchBehavior.None)]
        [Include(ObjectStage.Analysis)]
        IReadOnlyList<IReferenceSpan> References { get; }

        // TODO: Should this be just the symbol
        /// <summary>
        /// Definitions for the document. Sorted. No overlap?
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IDefinitionSpan> Definitions { get; }

        /// <summary>
        /// Classifications for the document. Sorted by start index. No overlap.
        /// </summary>
        [ReadOnlyList]
        [SearchBehavior(SearchBehavior.None)]
        [Include(ObjectStage.Analysis)]
        IReadOnlyList<IClassificationSpan> Classifications { get; }

        /// <summary>
        /// Outlining regions for the document. May overlap.
        /// </summary>
        [SearchBehavior(SearchBehavior.None)]
        IReadOnlyList<IOutliningRegion> OutliningRegions { get; }

        /// <summary>
        /// Indicates that the file should be excluded from text search
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool ExcludeFromSearch { get; }
    }

    /// <summary>
    /// Information about a source file as defined by the source control provider
    /// </summary>
    public interface ISourceControlFileInfo
    {
        /// <summary>
        /// Unique id for the source file content as defined by the source control provider (i.e. git SHA)
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string SourceControlContentId { get; }
    }

    public interface ISourceFileInfo : IRepoFileScopeEntity, ISourceControlFileInfo,
        // TODO: Remove and join source files by repository relative path with mapping from repository relative path to (project, project relative path)
        IProjectFileScopeEntity
    {
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
        /// TODO: Remove
        /// </summary>
        string Language { get; }

        /// <summary>
        /// The web address of the file. TODO: Remove? Is repo relative path enough?
        /// </summary>
        string WebAddress { get; }

        /// <summary>
        /// The encoding used for the file
        /// </summary>
        IEncodingDescription Encoding { get; }

        /// <summary>
        /// Extensible key value properties for the document.
        /// </summary>
        [Include(ObjectStage.Analysis)]
        IPropertyMap Properties { get; }
    }

    /// <summary>
    /// Describes encoding so that file may be reconstituted in a byte-identical form
    /// </summary>
    public interface IEncodingDescription
    {
        /// <summary>
        /// The name of the encoding
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The encoding preamble
        /// </summary>
        byte[] Preamble { get; }
    }

    public interface ISourceFileBase
    {
        /// <summary>
        /// The information about the source file
        /// </summary>
        ISourceFileInfo Info { get; }

        /// <summary>
        /// Indicates that the file should be excluded from text search
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        bool ExcludeFromSearch { get; }
    }

    /// <summary>
    /// Defines text contents of a file and associated data
    /// </summary>
    public interface ISourceFile : ISourceFileBase
    {
        /// <summary>
        /// The content of the file
        /// </summary>
        [SearchBehavior(SearchBehavior.FullText)]
        string Content { get; }
    }

    /// <summary>
    /// Defines text contents of a file and associated data
    /// </summary>
    public interface IChunkedSourceFile : ISourceFileBase
    {
        /// <summary>
        /// The content of the file
        /// </summary>
        IReadOnlyList<IChunkReference> Chunks { get; }
    }

    public interface IChunkReference
    {
        [SearchBehavior(SearchBehavior.Term)]
        string Id { get; }

        int StartLineNumber { get; }
    }

    /// <summary>
    /// Defines a chunk of text lines from a source file
    /// </summary>s
    public interface ISourceFileContentChunk
    {
        /// <summary>
        /// Lines defined as part of the chunk
        /// </summary>
        [SearchBehavior(SearchBehavior.FullText)]
        IReadOnlyList<string> ContentLines { get; }
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

    public interface IDefinitionSpan : ISpan
    {
        /// <summary>
        /// The definition symbol referred to by the span
        /// </summary>
        IDefinitionSymbol Definition { get; }

        /// <summary>
        /// Gets the definitions for parameters
        /// </summary>
        [ReadOnlyList]
        IReadOnlyList<IParameterDefinitionSpan> Parameters { get; }
    }

    /// <summary>
    /// A specialized definition span referring to a parameter of a method/property
    /// </summary>
    public interface IParameterDefinitionSpan : ILineSpan
    {
        // TODO: This is in theory implied from the ordering in IDefinitionSpan.Parameters. So no need
        // to serialize if its the same as the implied value
        /// <summary>
        /// The index of the parameter in the list of parameters for the method
        /// </summary>
        int ParameterIndex { get; }

        /// <summary>
        /// The name of the parameter
        /// </summary>
        string Name { get; }
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

        /// <summary>
        /// Gets the references to parameters
        /// </summary>
        [ReadOnlyList]
        IReadOnlyList<IParameterReferenceSpan> Parameters { get; }
    }

    /// <summary>
    /// A specialized reference span referring to a parameter to a method/property
    /// </summary>
    public interface IParameterReferenceSpan : ISymbolSpan
    {
        /// <summary>
        /// The index of the parameter in the list of parameters for the method
        /// </summary>
        [SearchBehavior(SearchBehavior.Term)]
        int ParameterIndex { get; }
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

        // TODO: Should locals be moved from here?
        /// <summary>
        /// The identifier to the local group of spans which refer to the same common symbol
        /// </summary>
        int LocalGroupId { get; }
    }

    public interface ISymbolSpan : ITextLineSpan
    {
    }

    public interface ITextLineSpan : ILineSpan
    {
        /// <summary>
        /// The line text
        /// TODO: It would be nice to not store this here and instead apply it as a join
        /// </summary>
        string LineSpanText { get; }
    }

    public interface ILineSpan : ISpan
    {
        /// <summary>
        /// The 0-based index of the line containing the span
        /// </summary>
        [Include(ObjectStage.None)]
        [CoerceGet(typeof(int?))]
        int LineIndex { get; }

        /// <summary>
        /// The 1-based line number of the line containing the span
        /// </summary>
        [CoerceGet(typeof(int?))]
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

    public static class SpanExtensions
    {
        public static int LineSpanEnd(this ILineSpan lineSpan)
        {
            return lineSpan.LineSpanStart + lineSpan.Length;
        }

        public static int End(this ISpan lineSpan)
        {
            return lineSpan.Start + lineSpan.Length;
        }

        public static bool SpanEquals(this ISpan span, ISpan otherSpan)
        {
            if (span == null)
            {
                return false;
            }

            return span.Start == otherSpan.Start && span.Length == otherSpan.Length;
        }

        public static bool Contains(this ISpan span, ISpan otherSpan)
        {
            if (span == null || otherSpan == null)
            {
                return false;
            }

            return otherSpan.Start >= span.Start && otherSpan.End() <= span.End();
        }
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
