using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IBoundSourceFile : IFileScopeEntity, ITextFile
    {
        IReadOnlyList<IReferenceSpan> References { get; }

        IReadOnlyList<IDefinitionSymbol> Definitions { get; }

        IReadOnlyList<IClassificationSpan> Classifications { get; }
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

    public interface IDefinitionSpan : ISymbolSpan<IDefinitionSymbol>
    {
    }

    public interface IReferenceSpan : ISymbolSpan<IReferenceSymbol>
    {
    }

    public interface IClassificationSpan : ISpan
    {
        int DefaultClassificationColor { get; }

        string Classification { get; }

        int LocalGroupId { get; }
    }

    public interface ISpan
    {
        int Start { get; }

        int Length { get; }
    }
}
