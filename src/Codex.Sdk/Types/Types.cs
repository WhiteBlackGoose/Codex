using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.ObjectModel;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel
{
    public partial class CodexTypeUtilities
    {
        public static Type GetInterfaceType(Type type)
        {
            if (!type.IsInterface)
            {
                s_typeMappings.TryGetValue(type, out var result);
                return result ?? type;
            }

            return type;
        }

        public static Type GetImplementationType(Type type)
        {
            if (type.IsInterface)
            {
                s_typeMappings.TryGetValue(type, out var result);
                return result ?? type;
            }

            return type;
        }
    }

    public partial class Symbol
    {
        /// <summary>
        /// Extension data used during analysis/search
        /// TODO: Why is this needed?
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

    public class PropertyMapBase : Dictionary<string, string>
    {
        public PropertyMapBase() : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }

    partial class PropertyMap : PropertyMapBase
    {
        protected void Initialize()
        {
        }
    }

    //partial class TextSourceSearchModel
    //{
    //    protected override void OnDeserializedCore()
    //    {
    //        base.OnDeserializedCore();
    //    }

    //    protected override void OnSerializingCore()
    //    {
    //        File.Content = 
    //        base.OnSerializingCore();
    //    }
    //}

    partial class ReferenceSearchModel
    {
        private IReadOnlyList<SymbolSpan> CoerceSpans(IReadOnlyList<SymbolSpan> value)
        {
            value = value ?? CompressedSpans?.ToList();
            this.Spans = value;
            return value;
        }

        protected override void OnSerializingCore()
        {
            if (Spans != null)
            {
                string lineSpanText = null;
                foreach (var span in Spans)
                {
                    span.LineSpanText = RemoveDuplicate(span.LineSpanText, ref lineSpanText);
                }
            }

            base.OnSerializingCore();
        }

        protected override void OnDeserializedCore()
        {
            if (Spans != null)
            {
                string lineSpanText = null;
                foreach (var span in Spans)
                {
                    span.LineSpanText = AssignDuplicate(span.LineSpanText, ref lineSpanText);
                }
            }

            base.OnDeserializedCore();
        }
    }
}

namespace Codex.Framework.Types
{
}
