using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class CompatibilityExtensions
    {
        public static string ToLowerInvariant(this string value)
        {
            return value.ToLower();
        }
    }
}

namespace Codex.ObjectModel
{
    partial struct SymbolId
    {
        [JsonConstructor]
        public SymbolId(string value)
        {
            Value = value;
        }
    }
}
