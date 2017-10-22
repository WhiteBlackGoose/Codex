using System;
using System.Collections.Generic;
using System.Text;
using Codex.ObjectModel;

namespace Codex
{
    public interface ISymbolLineSpanList
    {
        IReadOnlyList<SymbolSpan> ToList();
    }

    public interface IClassificationList
    {
        IReadOnlyList<ClassificationSpan> ToList();
    }

    public interface IReferenceList
    {
        IReadOnlyList<ReferenceSpan> ToList();
    }
}
