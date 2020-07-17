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
using Codex.ElasticSearch.Formats;

namespace Codex.Lucene.Framework
{
    public interface IOrderingTermStore
    {
        void ForEachTerm(Action<(BytesRef term, DocIdSet docs)> action);
        void Store(BytesRef term, DocIdSet docs);
    }

    public class MemoryOrderingTermStore : IOrderingTermStore
    {
        private readonly SortedDictionary<BytesRef, DocIdSet> _sortedTerms;

        public MemoryOrderingTermStore(IComparer<BytesRef> comparer)
        {
            _sortedTerms = new SortedDictionary<BytesRef, DocIdSet>(comparer);
        }

        public void ForEachTerm(Action<(BytesRef term, DocIdSet docs)> action)
        {
            foreach (var item in _sortedTerms)
            {
                action((item.Key, item.Value));
            }
        }

        public void Store(BytesRef term, DocIdSet docs)
        {
            _sortedTerms.Add(BytesRef.DeepCopyOf(term), new PForDeltaDocIdSet.Builder().Add(docs.GetIterator()).Build());
        }
    }
}
