using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

namespace Codex.ElasticSearch.Formats
{
    /// <summary>
    /// {@link DocIdSet} implementation inspired from http://roaringbitmap.org/
    /// <p>
    /// The space is divided into blocks of 2^16 bits and each block is encoded
    /// independently. In each block, if less than 2^12 bits are set, then
    /// documents are simply stored in a short[]. If more than 2^16-2^12 bits are
    /// set, then the inverse of the set is encoded in a simple short[]. Otherwise
    /// a {@link FixedBitSet} is used.
    /// 
    /// Ported from RoaringDocIdSet in Lucene.
    /// </summary>
    public class RoaringDocIdSet : DocIdSet
    {
        // Number of documents in a block
        private const int BLOCK_SIZE = 1 << 16;
        // The maximum length for an array, beyond that point we switch to a bitset
        private const int MAX_ARRAY_LENGTH = 1 << 12;
        private static readonly long BASE_RAM_BYTES_USED = RamUsageEstimator.ShallowSizeOfInstance(typeof(RoaringDocIdSet));

        public static readonly RoaringDocIdSet Empty = new Builder().Build();

        private enum DocIdSetType
        {
            NONE,
            SHORT_ARRAY,
            BIT
        }

        private readonly DocIdSet[] docIdSets;
        public readonly int Count;
        private RoaringDocIdSet(DocIdSet[] docIdSets, int cardinality)
        {
            this.docIdSets = docIdSets;
            this.Count = cardinality;
        }

        public bool Contains(int id)
        {
            int targetBlockIndex = id >> 16;
            if (targetBlockIndex >= docIdSets.Length)
            {
                return false;
            }

            var targetBlock = docIdSets[targetBlockIndex];
            if (targetBlock == null)
            {
                return false;
            }

            ushort subIndex = (ushort)(id & 0xFFFF);
            if (targetBlock is ShortArrayDocIdSet set)
            {
                return set.Contains(subIndex);
            }
            else if (targetBlock is FixedBitSet fixedSet)
            {
                return fixedSet.Get(subIndex);
            }

            throw new Exception("Unreachable");
        }

        public byte[] GetBytes()
        {
            GrowableByteArrayDataOutput output = new GrowableByteArrayDataOutput(1024);
            Write(output);

            byte[] result = new byte[output.Length];
            Array.Copy(output.Bytes, result, output.Length);

            // FOR DEBUGGING PURPOSES:
            //try
            //{
            //    FromBytes(result);
            //}
            //catch
            //{
            //    File.WriteAllLines($@"C:\temp\roardbg.{Guid.NewGuid().ToString().Substring(0, 8)}.txt", this.Enumerate().Select(s => s.ToString()));
            //    throw;
            //}

            return result;
        }

        public static RoaringDocIdSet FromBytes(byte[] bytes)
        {
            ByteArrayDataInput input = new ByteArrayDataInput(bytes);
            return Read(input);
        }

        public static RoaringDocIdSet From(IEnumerable<int> orderedIds)
        {
            var builder = new Builder();
            foreach (var id in orderedIds)
            {
                builder.Add(id);
            }

            return builder.Build();
        }

        public void Write(DataOutput output)
        {
            output.WriteVInt32(Count);
            output.WriteVInt32(docIdSets.Length);

            foreach (DocIdSet set in docIdSets)
            {
                if (set == null)
                {
                    output.WriteByte((byte)DocIdSetType.NONE);
                }
                else if (set is ShortArrayDocIdSet)
                {
                    output.WriteByte((byte)DocIdSetType.SHORT_ARRAY);
                    ShortArrayDocIdSet shortSet = (ShortArrayDocIdSet)set;
                    shortSet.Write(output);
                }
                else
                {
                    // set is FixedBitSet
                    output.WriteByte((byte)DocIdSetType.BIT);
                    FixedBitSet fixedBits = (FixedBitSet)set;
                    // TODO: This is serializing cost is not needed but we do so
                    // for compat with standard Lucene roaring doc id set
                    // This should be removed next time we compile elasticsearch
                    output.WriteVInt64(fixedBits.Cardinality());

                    long[] longs = fixedBits.GetBits();

                    output.WriteVInt32(fixedBits.Length);
                    output.WriteVInt32(longs.Length);

                    foreach (long l in longs)
                    {
                        output.WriteInt64(l);
                    }
                }
            }
        }

        public static RoaringDocIdSet Read(DataInput input)
        {
            int cardinality = input.ReadVInt32();
            int docIdSetsLength = input.ReadVInt32();

            DocIdSet[]
            docIdSets = new DocIdSet[docIdSetsLength];
            for (int i = 0; i < docIdSetsLength; i++)
            {
                DocIdSetType type = (DocIdSetType)input.ReadByte();
                if (type == DocIdSetType.NONE)
                {
                    docIdSets[i] = null;
                }
                else if (type == DocIdSetType.SHORT_ARRAY)
                {
                    docIdSets[i] = ShortArrayDocIdSet.read(input);
                }
                else
                {
                    // type == DocIdSetType.BITSET

                    int numBits = input.ReadVInt32();
                    int longsLength = input.ReadVInt32();

                    long[] longs = new long[longsLength];
                    for (int j = 0; j < longsLength; j++)
                    {
                        longs[j] = input.ReadInt64();
                    }

                    docIdSets[i] = new FixedBitSet(longs, numBits);
                }
            }

            return new RoaringDocIdSet(docIdSets, cardinality);
        }

        public override DocIdSetIterator GetIterator()
        {
            if (Count == 0)
            {
                return DocIdSetIterator.GetEmpty();
            }

            return new Iterator(this);
        }

        public IEnumerable<int> Enumerate()
        {
            var iterator = GetIterator();
            while (true)
            {
                var doc = iterator.NextDoc();
                if (doc == DocIdSetIterator.NO_MORE_DOCS)
                {
                    yield break;
                }

                yield return doc;
            }
        }

        private class Iterator : DocIdSetIterator
        {
            RoaringDocIdSet rdis;
            int block;
            DocIdSetIterator sub = null;
            int doc;

            public Iterator(RoaringDocIdSet rdis)
            {
                doc = -1;
                block = -1;
                this.rdis = rdis;
                sub = DocIdSetIterator.GetEmpty();
            }

            public override int DocID => doc;

            public override int NextDoc()
            {
                int subNext = sub.NextDoc();
                if (subNext == NO_MORE_DOCS)
                {
                    return firstDocFromNextBlock();
                }
                return doc = (block << 16) | subNext;
            }


            public override int Advance(int target)
            {
                int targetBlock = target >> 16;
                if (targetBlock != block)
                {
                    block = targetBlock;
                    if (block >= rdis.docIdSets.Length)
                    {
                        sub = null;
                        return doc = NO_MORE_DOCS;
                    }
                    if (rdis.docIdSets[block] == null)
                    {
                        return firstDocFromNextBlock();
                    }
                    sub = rdis.docIdSets[block].GetIterator();
                }

                int subNext = sub.Advance(target & 0xFFFF);
                if (subNext == NO_MORE_DOCS)
                {
                    return firstDocFromNextBlock();
                }
                return doc = (block << 16) | subNext;
            }

            private int firstDocFromNextBlock()
            {
                while (true)
                {
                    block += 1;
                    if (block >= rdis.docIdSets.Length)
                    {
                        sub = null;
                        return doc = NO_MORE_DOCS;
                    }
                    else if (rdis.docIdSets[block] != null)
                    {
                        sub = rdis.docIdSets[block].GetIterator();
                        int subNext = sub.NextDoc();
                        Contract.Assert(subNext != NO_MORE_DOCS);
                        return doc = (block << 16) | subNext;
                    }
                }
            }


            public override long GetCost()
            {
                return rdis.Count;
            }
        }

        /**
         * Return the exact number of documents that are contained in this set.
         */
        public int Cardinality()
        {
            return Count;
        }

        public override String ToString()
        {
            return "RoaringDocIdSet(cardinality=" + Count + ")";
        }

        /**
         * A builder of {@link RoaringDocIdSet}s.
         */
        public class Builder
        {
            private readonly List<DocIdSet> sets = new List<DocIdSet>();

            private int cardinality;
            private int lastDocId;
            private int currentBlock;
            private int currentBlockCardinality;

            // We start by filling the buffer and when it's full we copy the content of
            // the buffer to the FixedBitSet and put further documents in that bitset
            private readonly ushort[] buffer;
            private FixedBitSet denseBuffer;

            /**
             * Sole constructor.
             */
            public Builder()
            {
                lastDocId = -1;
                currentBlock = -1;
                buffer = new ushort[MAX_ARRAY_LENGTH];
            }

            private void EnsureCapacity()
            {
                for (int i = sets.Count; i <= currentBlock; i++)
                {
                    sets.Add(null);
                }
            }

            private void Flush()
            {
                Contract.Assert(currentBlockCardinality <= BLOCK_SIZE);

                EnsureCapacity();
                if (currentBlockCardinality <= MAX_ARRAY_LENGTH)
                {
                    // Use sparse encoding
                    Contract.Assert(denseBuffer == null);
                    if (currentBlockCardinality > 0)
                    {
                        var blockBuffer = new ushort[currentBlockCardinality];
                        Array.Copy(buffer, blockBuffer, currentBlockCardinality);
                        sets[currentBlock] = new ShortArrayDocIdSet(blockBuffer, false);
                    }
                }
                else
                {
                    Contract.Assert(denseBuffer != null);
                    Contract.Assert(denseBuffer.Cardinality() == currentBlockCardinality);
                    if (denseBuffer.Length == BLOCK_SIZE && BLOCK_SIZE - currentBlockCardinality < MAX_ARRAY_LENGTH)
                    {
                        // Doc ids are very dense, inverse the encoding
                        ushort[] excludedDocs = new ushort[BLOCK_SIZE - currentBlockCardinality];
                        denseBuffer.Flip(0, denseBuffer.Length);
                        int excludedDoc = -1;
                        for (int i = 0; i < excludedDocs.Length; ++i)
                        {
                            excludedDoc = denseBuffer.NextSetBit(excludedDoc + 1);
                            Contract.Assert(excludedDoc != DocIdSetIterator.NO_MORE_DOCS);
                            excludedDocs[i] = (ushort)excludedDoc;
                        }
                        Contract.Assert(excludedDoc + 1 == denseBuffer.Length || denseBuffer.NextSetBit(excludedDoc + 1) == -1);
                        sets[currentBlock] = new ShortArrayDocIdSet(excludedDocs, true);
                    }
                    else
                    {
                        // Neither sparse nor super dense, use a fixed bit set
                        sets[currentBlock] = denseBuffer;
                    }
                    denseBuffer = null;
                }

                cardinality += currentBlockCardinality;
                denseBuffer = null;
                currentBlockCardinality = 0;
            }

            /**
             * Add a new doc-id to this builder.
             * NOTE: doc ids must be added in order.
             */
            public Builder Add(int docId)
            {
                if (docId <= lastDocId)
                {
                    throw new ArgumentOutOfRangeException("Doc ids must be added in-order, got " + docId + " which is <= lastDocID=" + lastDocId);
                }

                int block = docId >> 16;
                if (block != currentBlock)
                {
                    // we went to a different block, let's flush what we buffered and start from fresh
                    Flush();
                    currentBlock = block;
                }

                if (currentBlockCardinality < MAX_ARRAY_LENGTH)
                {
                    buffer[currentBlockCardinality] = (ushort)docId;
                }
                else
                {
                    if (denseBuffer == null)
                    {
                        // the buffer is full, let's move to a fixed bit set
                        denseBuffer = new FixedBitSet(1 << 16);
                        foreach (short doc in buffer)
                        {
                            denseBuffer.Set(doc & 0xFFFF);
                        }
                    }
                    denseBuffer.Set(docId & 0xFFFF);
                }

                lastDocId = docId;
                currentBlockCardinality += 1;
                return this;
            }

            /**
             * Add the content of the provided {@link DocIdSetIterator}.
             */
            public Builder Add(DocIdSetIterator disi, int offset = 0)
            {
                for (int doc = disi.NextDoc(); doc != DocIdSetIterator.NO_MORE_DOCS; doc = disi.NextDoc())
                {
                    Add(doc + offset);
                }
                return this;
            }

            /**
             * Build an instance.
             */
            public RoaringDocIdSet Build()
            {
                Flush();
                return new RoaringDocIdSet(sets.ToArray(), cardinality);
            }

        }

        /**
         * {@link DocIdSet} implementation that can store documents up to 2^16-1 in a short[].
         */
        private class ShortArrayDocIdSet : DocIdSet
        {
            private static readonly long BASE_RAM_BYTES_USED = RamUsageEstimator.ShallowSizeOfInstance(typeof(ShortArrayDocIdSet));

            private readonly ushort[] docIDs;
            private readonly bool invert;

            public ShortArrayDocIdSet(ushort[] docIDs, bool invert)
            {
                this.docIDs = docIDs;
                this.invert = invert;
            }

            public bool Contains(ushort index)
            {
                var found = Array.BinarySearch(docIDs, index) >= 0;
                return invert ? !found : found;
            }

            public void Write(DataOutput output)
            {
                output.WriteByte((byte)(invert ? 1 : 0));
                output.WriteVInt32(docIDs.Length);

                ushort last = 0;
                foreach (ushort id in docIDs)
                {
                    output.WriteVInt32(id - last);
                    last = id;
                }
            }

            public static ShortArrayDocIdSet read(DataInput input)
            {
                bool invert = input.ReadByte() == 1;
                int docIDsLength = input.ReadVInt32();
                ushort[] docIDs = new ushort[docIDsLength];

                ushort last = 0;
                for (int i = 0; i < docIDsLength; i++)
                {
                    last += (ushort)input.ReadVInt32();
                    docIDs[i] = last;
                }

                return new ShortArrayDocIdSet(docIDs, invert);
            }

            public override DocIdSetIterator GetIterator()
            {
                DocIdSetIterator iterator = new CoreIterator(this);
                if (!invert)
                {
                    return iterator;
                }
                else
                {
                    return new InverseIterator(this, iterator);
                }
            }

            // Copied from NotDocIdSet
            private class InverseIterator : DocIdSetIterator
            {
                int doc = -1;
                int nextSkippedDoc = -1;
                const int MAX_DOC = BLOCK_SIZE;
                private readonly ShortArrayDocIdSet set;
                private readonly DocIdSetIterator iterator;

                public InverseIterator(ShortArrayDocIdSet set, DocIdSetIterator iterator)
                {
                    this.set = set;
                    this.iterator = iterator;
                }

                private int docId(int i)
                {
                    return set.docIDs[i] & 0xFFFF;
                }

                public override int NextDoc()
                {
                    return Advance(doc + 1);
                }

                public override int DocID => doc;

                public override long GetCost()
                {
                    return MAX_DOC - set.docIDs.Length;
                }

                public override int Advance(int target)
                {
                    doc = target;
                    if (doc > nextSkippedDoc)
                    {
                        nextSkippedDoc = iterator.Advance(doc);
                    }
                    while (true)
                    {
                        if (doc >= MAX_DOC)
                        {
                            return doc = NO_MORE_DOCS;
                        }
                        Contract.Assert(doc <= nextSkippedDoc);
                        if (doc != nextSkippedDoc)
                        {
                            return doc;
                        }
                        doc += 1;
                        nextSkippedDoc = iterator.NextDoc();
                    }
                }
            }

            private class CoreIterator : DocIdSetIterator
            {
                int i = -1; // this is the index of the current document in the array
                int doc = -1;
                private readonly ShortArrayDocIdSet set;

                public CoreIterator(ShortArrayDocIdSet set)
                {
                    this.set = set;
                }

                private int docId(int i)
                {
                    return set.docIDs[i] & 0xFFFF;
                }

                public override int NextDoc()
                {
                    if (++i >= set.docIDs.Length)
                    {
                        return doc = NO_MORE_DOCS;
                    }
                    return doc = docId(i);
                }

                public override int DocID => doc;

                public override long GetCost()
                {
                    return set.docIDs.Length;
                }

                public override int Advance(int target)
                {
                    // binary search
                    int lo = i + 1;
                    int hi = set.docIDs.Length - 1;
                    while (lo <= hi)
                    {
                        int mid = (lo + hi) >> 1;
                        int midDoc = docId(mid);
                        if (midDoc < target)
                        {
                            lo = mid + 1;
                        }
                        else
                        {
                            hi = mid - 1;
                        }
                    }
                    if (lo == set.docIDs.Length)
                    {
                        i = set.docIDs.Length;
                        return doc = NO_MORE_DOCS;
                    }
                    else
                    {
                        i = lo;
                        return doc = docId(i);
                    }
                }
            }
        }
    }
}
