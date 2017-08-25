using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IBoundSourceFile : IFileScopeEntity
    {
        [SearchBehavior(SearchBehavior.FullText)]
        string Content { get; }

        IReadOnlyList<IReferenceSpan> References { get; }

        IReadOnlyList<IDefinitionSpan> Definitions { get; }

        IReadOnlyList<IClassificationSpan> Classifications { get; }
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

    public interface ISymbolSpan : ISpan
    {

    }

    public interface ILineSpan
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
