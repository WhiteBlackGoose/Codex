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
using System.Diagnostics;

namespace Codex.Lucene.Framework.AutoPrefix
{
    public class AutoPrefixTermsConsumer : TermsConsumer
    {
        private readonly IOrderingTermStore termStore;
        private readonly TermsConsumer inner;

        public override IComparer<BytesRef> Comparer => inner.Comparer;

        private AutoPrefixNode currentNode;

        public AutoPrefixTermsConsumer(TermsConsumer inner, IOrderingTermStore termStore, int docCount)
        {
            this.inner = inner;
            this.termStore = termStore;
            currentNode = new AutoPrefixNode(docCount, null);
        }

        public override void Finish(long sumTotalTermFreq, long sumDocFreq, int docCount)
        {
            while (currentNode.Term.Length > 0)
            {
                PopNode();
            }

            termStore.ForEachTerm(t =>
            {
                var consumer = inner.StartTerm(t.term);

                int count = 0;
                foreach (var doc in t.docs.Enumerate())
                {
                    count++;
                    consumer.StartDoc(doc, -1);
                    consumer.FinishDoc();
                }

                Print($"Finish.FinishTerm {count}", t.term);
                inner.FinishTerm(t.term, new TermStats(count, count));
            });
        }

        public override void FinishTerm(BytesRef text, TermStats stats)
        {
        }

        public override PostingsConsumer StartTerm(BytesRef textRef)
        {
            BytesRefString text = textRef;
            Print("Start", text);
            Print("StartNode", currentNode);
            BytesRefString commonPrefix = currentNode.GetCommonPrefix(text);
            Print("CommonPrefix", commonPrefix);

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

            Print("BeforePush", currentNode);
            currentNode = currentNode.Push(text);
            Print("AfterPush", currentNode);
            return currentNode;
        }

        private void PersistNode()
        {
            Print("Persist", currentNode);
            termStore.Store(currentNode.Term, currentNode.Docs);
        }

        private void PopNode()
        {
            PersistNode();
            Print("BeforePop", currentNode);
            currentNode.Pop();
            currentNode = currentNode.Prior;
            Print("AfterPop", currentNode);
        }

        [Conditional("DEBUG")]
        private void Print(string message, AutoPrefixNode node)
        {
            Print($"{message} #:{node?.Height}", node?.Term);
        }

        [Conditional("DEBUG")]
        private void Print(string message, BytesRefString? term)
        {
            System.Diagnostics.Debug.WriteLine($"{message} '{term}'");
            Console.WriteLine($"{message.PadRight(20, ' ')} '{term}'");
        }
    }
}
