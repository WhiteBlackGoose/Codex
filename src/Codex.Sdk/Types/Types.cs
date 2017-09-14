using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.ObjectModel
{
    public partial class Symbol
    {
        /// <summary>
        /// Extension data used during analysis/search
        /// </summary>
        public ExtensionData ExtData { get; set; }

        protected bool Equals(Symbol other)
        {
            return string.Equals(ProjectId, other.ProjectId, StringComparison.Ordinal) && string.Equals(Id.Value, other.Id.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Symbol)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ProjectId?.GetHashCode() ?? 0) * 397) ^ (Id.Value?.GetHashCode() ?? 0);
            }
        }

        public override string ToString()
        {
            return Id.Value;
        }
    }

    partial class ReferenceSymbol : Symbol
    {
        public override string ToString()
        {
            return ReferenceKind + " " + base.ToString();
        }
    }

    partial class DefinitionSymbol
    {
        //public string ShortName
        //{
        //    get
        //    {
        //        return shortName ?? "";
        //    }
        //    set
        //    {
        //        shortName = value;
        //    }
        //}


        public int ReferenceCount;

        protected override void Initialize()
        {
            ReferenceKind = nameof(ObjectModel.ReferenceKind.Definition);
            base.Initialize();
        }

        private string CoerceShortName(string value)
        {
            return value ?? "";
        }

        public void IncrementReferenceCount()
        {
            Interlocked.Increment(ref ReferenceCount);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public partial class Span
    {
        /// <summary>
        /// The absolute character offset of the end (exclusive) of the span within the document
        /// </summary>
        public int End => Start + Length;

        public bool Contains(Span span)
        {
            if (span == null)
            {
                return false;
            }

            return span.Start >= Start && span.End <= End;
        }

        public bool SpanEquals(Span span)
        {
            if (span == null)
            {
                return false;
            }

            return span.Start == Start && span.End == End;
        }
    }

    partial class ClassificationSpan
    {
        protected override void Initialize()
        {
            DefaultClassificationColor = -1;
            base.Initialize();
        }
    }

    partial class SymbolSpan
    {
        public int LineSpanEnd => LineSpanStart + Length;

        public void Trim()
        {
            var initialLength = LineSpanText.Length;
            LineSpanText = LineSpanText.TrimStart();
            var newLength = LineSpanText.Length;
            LineSpanStart -= (initialLength - newLength);
            LineSpanText = LineSpanText.TrimEnd();
            LineSpanStart = Math.Max(LineSpanStart, 0);
            Length = Math.Min(LineSpanText.Length, Length);
        }

        public ReferenceSpan CreateReference(ReferenceSymbol referenceSymbol, SymbolId relatedDefinition = default(SymbolId))
        {
            return new ReferenceSpan(this)
            {
                RelatedDefinition = relatedDefinition,
                Reference = referenceSymbol
            };
        }

        public DefinitionSpan CreateDefinition(DefinitionSymbol definition)
        {
            return new DefinitionSpan(this)
            {
                Definition = definition
            };
        }
    }

    partial class SourceFileInfo
    {
        /// <summary>
        /// Extensible key value properties for the document. TODO: Move to type definition
        /// </summary>
        public Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

namespace Codex.Framework.Types
{
}
