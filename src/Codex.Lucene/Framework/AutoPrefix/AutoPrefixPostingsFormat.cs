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
using Lucene.Net.Codecs.Lucene41;

namespace Codex.Lucene.Framework.AutoPrefix
{
    [PostingsFormatName("Lucene41")]
    public class AutoPrefixPostingsFormat : PostingsFormat
    {
        private readonly PostingsFormat inner;

        public AutoPrefixPostingsFormat(PostingsFormat inner)
        {
            this.inner = inner;
        }

        public override FieldsConsumer FieldsConsumer(SegmentWriteState state)
        {
            return new AutoPrefixFieldsConsumer(state, inner.FieldsConsumer(state));
        }

        public override FieldsProducer FieldsProducer(SegmentReadState state)
        {
            // TODO: Need some special logic to only return full terms for sake of merge
            return inner.FieldsProducer(state);
        }
    }
}
