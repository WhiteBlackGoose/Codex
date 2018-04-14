using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public interface ISourceFile
    {
        SourceFileInfo Info { get; }
        Task<string> GetContentsAsync();
    }

    public class EncodedString
    {
        public readonly string Value;
        public readonly Encoding Encoding;

        public EncodedString(string value, Encoding encoding)
        {
            Value = value;
            Encoding = encoding;
        }

        public static implicit operator string(EncodedString value)
        {
            return value.Value;
        }
    }

    public struct CaseInsensitiveString : IEquatable<CaseInsensitiveString>, IComparable<CaseInsensitiveString>
    {
        public readonly string Value;

        public CaseInsensitiveString(string value)
        {
            Value = value;
        }

        public int CompareTo(CaseInsensitiveString other)
        {
            return StringComparer.OrdinalIgnoreCase.Compare(this.Value, other.Value);
        }

        public bool Equals(CaseInsensitiveString other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(this.Value, other.Value);
        }

        public static implicit operator string(CaseInsensitiveString value)
        {
            return value.Value;
        }
    }

    public class TextReferenceEntry
    {
        public string ReferringProjectId { get; set; }
        public string ReferringFilePath { get; set; }
        public SymbolSpan ReferringSpan { get; set; }

        public string File => ReferringFilePath;
        public SymbolSpan Span => ReferringSpan;
    }

    public class SymbolReferenceEntry
    {
        public string ReferringProjectId { get; set; }
        public string ReferringFilePath { get; set; }
        public ReferenceSpan ReferringSpan { get; set; }

        public string File => ReferringFilePath;
        public ReferenceSpan Span => ReferringSpan;
    }

    public class SymbolSearchResultEntry
    {
        public DefinitionSymbol Symbol => Span.Definition;
        public DefinitionSpan Span { get; set; }
        public string File { get; set; }
        public string Glyph { get; set; }
        public int Rank { get; set; }
        public int KindRank { get; set; }
        public int MatchLevel { get; set; }
        public string ReferenceKind { get; set; } = nameof(ObjectModel.ReferenceKind.Definition);
        public string DisplayName => Symbol?.DisplayName ?? File;
    }

    public class GetDefinitionResult
    {
        public SourceFileInfo File { get; set; }
        public DefinitionSpan Span { get; set; }
    }

    public class ProjectContents
    {
        public string Id { get; set; }
        public DateTime DateUploaded { get; set; }

        public int SourceLineCount { get; set; }
        public int SymbolCount { get; set; }

        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public List<ReferencedProject> References { get; set; } = new List<ReferencedProject>();
        public List<SourceFileInfo> Files { get; set; } = new List<SourceFileInfo>();
    }

    public class SymbolSearchResult
    {
        public List<SymbolSearchResultEntry> Entries { get; set; }

        public int Total { get; set; }
        public string QueryText { get; set; }

        public string Error { get; set; }
    }

    public class SymbolReferenceResult
    {
        public List<SymbolReferenceEntry> Entries { get; set; }

        public IList<SymbolSearchResultEntry> RelatedDefinitions { get; set; }

        public int Total { get; set; }
        public string SymbolName { get; set; }
        public string ProjectId { get; set; }
        public string SymbolId { get; set; }
    }
}
