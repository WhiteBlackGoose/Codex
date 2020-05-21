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

namespace Codex.Lucene.Framework
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
        }

        protected override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }

        private class AutoPrefixTermsConsumer : TermsConsumer
        {
            private readonly TermsConsumer inner;

            public override IComparer<BytesRef> Comparer => inner.Comparer;

            public override void Finish(long sumTotalTermFreq, long sumDocFreq, int docCount)
            {
                throw new NotImplementedException();
            }

            public override void FinishTerm(BytesRef text, TermStats stats)
            {
                throw new NotImplementedException();
            }

            public override PostingsConsumer StartTerm(BytesRef text)
            {
                throw new NotImplementedException();
            }
        }

        private class AutoPrefixPostingsConsumer : PostingsConsumer
        {
            private readonly PostingsConsumer inner;

            public override void AddPosition(int position, BytesRef payload, int startOffset, int endOffset)
            {
                throw new NotImplementedException();
            }

            public override void FinishDoc()
            {
                throw new NotImplementedException();
            }

            public override void StartDoc(int docId, int freq)
            {
                throw new NotImplementedException();
            }
        }
    }
}
