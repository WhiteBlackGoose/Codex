using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.ObjectModel
{
    public class SymbolSearchResult
    {
        public List<SymbolSearchResultEntry> Entries { get; set; }

        public int Total { get; set; }
        public string QueryText { get; set; }

        public string Error { get; set; }
    }

    public class SymbolSearchResultEntry
    {
        public IDefinitionSymbol Symbol { get; set; }
        public string File { get; set; }
        public string Glyph { get; set; }
        public int Rank { get; set; }
        public int KindRank { get; set; }
        public int MatchLevel { get; set; }
        public string ReferenceKind { get; set; } = nameof(ObjectModel.ReferenceKind.Definition);
        public string DisplayName => Symbol?.DisplayName ?? File;
    }
}
