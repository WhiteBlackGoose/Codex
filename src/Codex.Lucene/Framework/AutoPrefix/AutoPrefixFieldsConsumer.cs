using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Codecs.PerField;
using Lucene.Net.Codecs;
using Lucene.Net.Codecs.Lucene46;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public class AutoPrefixFieldsConsumer : FieldsConsumer
    {
        private readonly FieldsConsumer inner;
        private readonly SegmentWriteState state;

        public AutoPrefixFieldsConsumer(SegmentWriteState state, FieldsConsumer inner)
        {
            this.state = state;
            this.inner = inner;
        }

        public override TermsConsumer AddField(FieldInfo field)
        {
            var innerTermsConsumer = inner.AddField(field);
            return new AutoPrefixTermsConsumer(innerTermsConsumer, CreateTermStore(field, innerTermsConsumer.Comparer), state.SegmentInfo.DocCount);
        }

        private IOrderingTermStore CreateTermStore(FieldInfo field, IComparer<BytesRef> comparer)
        {
            return new MemoryOrderingTermStore(comparer);
        }

        protected override void Dispose(bool disposing)
        {
            inner.Dispose();
        }
    }
}
