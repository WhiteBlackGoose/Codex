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
            return new AutoPrefixTermsConsumer(inner.AddField(field), CreateTermStore(field), state.SegmentInfo.DocCount);
        }

        private IOrderingTermStore CreateTermStore(FieldInfo field)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            throw new NotImplementedException();
        }
    }
}
