using Codex.Utilities;
using System;

namespace Codex.ObjectModel
{
    public partial struct SymbolId : IEquatable<SymbolId>
    {
        public readonly string Value;

        private SymbolId(string value, bool ignored)
        {
            Value = value;
        }

        public static SymbolId UnsafeCreateWithValue(string value)
        {
            return new SymbolId(value, true);
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
