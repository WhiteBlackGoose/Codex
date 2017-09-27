using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    [Placeholder]
    public class SymbolId
    {
    }

    [Placeholder]
    public class Glyph
    {
    }

    [Placeholder]
    public class SymbolSpan
    {
    }

    [Placeholder]
    public class ClassificationSpan
    {
    }
}

namespace Codex.Utilities
{
    public static class CollectionUtilities
    {
        public class Empty<T>
        {
            public static readonly T[] Array = new T[0];
        }
    }
}
