using Codex.Utilities;
using System;

namespace Codex.ObjectModel
{
    public struct SymbolId : IEquatable<SymbolId>
    {
        public readonly string Value;

        private SymbolId(string value)
        {
            Value = value;
        }

        public static SymbolId CreateFromId(string id)
        {
            // return new SymbolId(id);
            return new SymbolId(IndexingUtilities.ComputeSymbolUid(id));
        }

        public static SymbolId UnsafeCreateWithValue(string value)
        {
            return new SymbolId(value);
        }

        public bool Equals(SymbolId other)
        {
            return Value == other.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
