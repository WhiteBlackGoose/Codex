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
    public class AutoPrefixNode : PostingsConsumer
    {
        public BytesRef Term;
        public int NumTerms;
        private OpenBitSet builder;

        public DocIdSet Docs => builder;

        private readonly int docCount;

        public readonly AutoPrefixNode Prior;
        public AutoPrefixNode Next;

        public AutoPrefixNode(int docCount, AutoPrefixNode prior)
        {
            Prior = prior;
            this.docCount = docCount;
            this.builder = new OpenBitSet(docCount);
        }

        public AutoPrefixNode Push(BytesRef term)
        {
            if (Next != null)
            {
                Next.Reset(term);
            }
            else
            {
                Next = new AutoPrefixNode(docCount, this);
            }

            return Next;
        }

        public AutoPrefixNode Pop()
        {
            Prior.Add(builder);
            return this;
        }

        public void Reset(BytesRef term)
        {
            NumTerms = 0;
            Term = term;
            this.builder = new OpenBitSet(docCount);
        }

        internal BytesRef GetCommonPrefix(BytesRef text)
        {
            throw new NotImplementedException();
        }

        public void Add(OpenBitSet bitSet)
        {
            builder.Union(bitSet);
        }

        public override void StartDoc(int docId, int freq)
        {
            builder.FastSet(docId);
        }

        public override void AddPosition(int position, BytesRef payload, int startOffset, int endOffset)
        {
        }

        public override void FinishDoc()
        {
        }
    }
}
