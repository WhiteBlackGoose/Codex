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
    public readonly struct BytesRefString
    {
        public BytesRef Value { get; }

        public int Length => Value.Length;

        public byte[] Bytes => Value.Bytes;

        public byte this[int i] => Bytes[i + Value.Offset];

        public BytesRefString(BytesRef value)
        {
            Value = value;
        }

        public static implicit operator BytesRefString(BytesRef value)
        {
            return new BytesRefString(value);
        }

        public static implicit operator BytesRef(BytesRefString value)
        {
            return value.Value;
        }

        public override string ToString()
        {
            return $"{Value?.Utf8ToString()} [{Value?.Length ?? -1}]";
        }
    }

    public class AutoPrefixNode : PostingsConsumer
    {
        private static readonly BytesRef Empty = new BytesRef();
        public BytesRefString Term = Empty;
        public int NumTerms;
        public int Height;
        private OpenBitSet builder;

        public DocIdSet Docs => builder;

        private readonly int docCount;

        public readonly AutoPrefixNode Prior;
        public AutoPrefixNode Next;

        public AutoPrefixNode(int docCount, AutoPrefixNode prior)
        {
            Prior = prior;
            this.docCount = docCount;
            Height = (prior?.Height ?? 0) + 1;
            this.builder = new OpenBitSet(docCount);
        }

        public AutoPrefixNode Push(BytesRefString term)
        {
            if (Next != null)
            {
                Next.Reset(term);
            }
            else
            {
                Next = new AutoPrefixNode(docCount, this);
                Next.Term = BytesRef.DeepCopyOf(term);
            }

            return Next;
        }

        public AutoPrefixNode Pop()
        {
            Prior.Add(builder);
            return this;
        }

        public void Reset(BytesRefString term)
        {
            NumTerms = 0;
            if (Term.Value.Bytes.Length < term.Length)
            {
                Term.Value.Bytes = new byte[term.Length * 2];
            }

            Array.Copy(term.Bytes, term.Value.Offset, Term.Value.Bytes, 0, term.Length);
            Term.Value.Length = term.Length;

            this.builder = new OpenBitSet(docCount);
        }

        internal BytesRefString GetCommonPrefix(BytesRefString text)
        {
            var length = Math.Min(text.Length, Term.Length);
            var commonLength = 0;
            for (commonLength = 0; commonLength < length; commonLength++)
            {
                if (text[commonLength] != Term[commonLength])
                {
                    break;
                }
            }

            if (commonLength == 0) return Empty;

            var bytes = new byte[commonLength];
            Array.Copy(text.Bytes, text.Value.Offset, bytes, 0, commonLength);
            return new BytesRef(bytes);
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
