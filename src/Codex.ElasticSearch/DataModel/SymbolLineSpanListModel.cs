using System.Collections.Generic;
using System.Runtime.Serialization;
using Codex.ObjectModel;
using Codex.Utilities;
using Newtonsoft.Json;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.Storage.DataModel
{
    public class SymbolLineSpanListModel : SpanListModel<SymbolSpan, SpanListSegmentModel, SymbolSpan, int>, ISymbolLineSpanList
    {
        public static readonly IComparer<SymbolSpan> SharedSymbolLineModelComparer = new ComparerBuilder<SymbolSpan>()
            .CompareByAfter(s => s.LineSpanText);

        public SymbolLineSpanListModel()
        {
            Optimize = false;
        }

        public SymbolLineSpanListModel(IReadOnlyList<SymbolSpan> spans)
            : base(spans, sharedValueSorter: SharedSymbolLineModelComparer)
        {
            Optimize = false;
        }

        public override SpanListSegmentModel CreateSegment(ListSegment<SymbolSpan> segmentSpans)
        {
            return new SpanListSegmentModel();
        }

        public override SymbolSpan CreateSpan(int start, int length, SymbolSpan shared, SpanListSegmentModel segment, int segmentOffset)
        {
            return new SymbolSpan()
            {
                Start = shared.Start + start,
                LineSpanStart = start,
                Length = length,
                LineSpanText = shared.LineSpanText,
                LineIndex = shared.LineIndex
            };
        }

        public override SymbolSpan GetShared(SymbolSpan span)
        {
            return new SymbolSpan()
            {
                LineSpanText = span.LineSpanText,
                LineIndex = span.LineIndex,
                Start = span.Start - span.LineSpanStart
            };
        }

        public override int GetStart(SymbolSpan span, SymbolSpan shared)
        {
            return span.Start - shared.Start;
        }

        public override int GetSharedKey(SymbolSpan span)
        {
            return span.LineIndex;
        }

        [OnSerializing]
        public void PostProcessReferences(StreamingContext context)
        {
            string lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = RemoveDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }

        [OnDeserialized]
        public void MakeReferences(StreamingContext context)
        {
            string lineSpanText = null;
            foreach (var symbolLine in SharedValues)
            {
                symbolLine.LineSpanText = AssignDuplicate(symbolLine.LineSpanText, ref lineSpanText);
            }
        }
    }
}
