using Codex.Utilities;
using System;

namespace Codex.ObjectModel
{
    public partial struct SymbolId
    {
        public static SymbolId CreateFromId(string id)
        {
            // return new SymbolId(id);
            return new SymbolId(IndexingUtilities.ComputeSymbolUid(id), true);
        }
    }
}
