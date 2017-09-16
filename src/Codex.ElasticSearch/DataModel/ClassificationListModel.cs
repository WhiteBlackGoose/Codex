using Codex.ObjectModel;
using Codex.Storage.Utilities;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Codex.Storage.Utilities.NumberUtils;
using System.Collections;
using Codex.Utilities;

namespace Codex.Storage.DataModel
{
    public class ClassificationListModel : SpanListModel<ClassificationSpan, ClassificationSpanListSegmentModel, ClassificationStyle, string>, IClassificationList
    {
        public ClassificationListModel()
        {
        }

        public ClassificationListModel(IReadOnlyList<ClassificationSpan> spans)
            : base(spans)
        {
        }

        public override ClassificationSpanListSegmentModel CreateSegment(ListSegment<ClassificationSpan> segmentSpans)
        {
            return new ClassificationSpanListSegmentModel()
            {
                LocalSymbolGroupIds = IntegerListModel.Create(segmentSpans, span => span.LocalGroupId)
            };
        }

        public override ClassificationSpan CreateSpan(int start, int length, ClassificationStyle shared, ClassificationSpanListSegmentModel segment, int segmentOffset)
        {
            return new ClassificationSpan()
            {
                Start = start,
                Length = length,
                Classification = shared.Name,
                DefaultClassificationColor = shared.Color,
                LocalGroupId = segment.LocalSymbolGroupIds?[segmentOffset] ?? 0
            };
        }

        public override ClassificationStyle GetShared(ClassificationSpan span)
        {
            return new ClassificationStyle()
            {
                Name = span.Classification,
                Color = span.DefaultClassificationColor
            };
        }

        public override string GetSharedKey(ClassificationSpan span)
        {
            return span.Classification;
        }
    }

    public class ClassificationSpanListSegmentModel : SpanListSegmentModel
    {
        public IntegerListModel LocalSymbolGroupIds { get; set; }

        internal override void OptimizeLists(OptimizationContext context)
        {
            LocalSymbolGroupIds?.Optimize(context);

            base.OptimizeLists(context);
        }

        internal override void ExpandLists(OptimizationContext context)
        {
            LocalSymbolGroupIds?.ExpandData(context);

            base.ExpandLists(context);
        }
    }
}
