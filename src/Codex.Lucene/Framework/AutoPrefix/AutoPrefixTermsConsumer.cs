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
using Lucene.Net.Util.Packed;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public class AutoPrefixTermsConsumer : TermsConsumer
    {
        private readonly IOrderingTermStore termStore;
        private readonly TermsConsumer inner;
        private readonly int docCount;

        public override IComparer<BytesRef> Comparer => inner.Comparer;

        private AutoPrefixNode currentNode;

        public override void Finish(long sumTotalTermFreq, long sumDocFreq, int docCount)
        {
            while (currentNode.Term.Length > 0)
            {
                PopNode();
            }
        }

        public override void FinishTerm(BytesRef text, TermStats stats)
        {
        }

        public override PostingsConsumer StartTerm(BytesRef text)
        {
            BytesRef commonPrefix = currentNode.GetCommonPrefix(text);

            while (commonPrefix.Length < currentNode.Term.Length)
            {
                var priorNodeLength = currentNode.Prior?.Term.Length;
                if (commonPrefix.Length > priorNodeLength)
                {
                    PersistNode();
                    currentNode.Term = commonPrefix;
                    break;
                }

                PopNode();
            }

            currentNode = currentNode.Push(text);
            return currentNode;
        }

        private void PersistNode()
        {
            termStore.Store(currentNode.Term, currentNode.Docs);
        }

        private void PopNode()
        {
            PersistNode();
            currentNode.Pop();
            currentNode = currentNode.Prior;
        }
    }

    public interface IOrderingTermStore
    {
        void Store(BytesRef term, DocIdSet docs);
    }
}
