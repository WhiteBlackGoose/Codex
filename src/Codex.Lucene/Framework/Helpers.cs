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

namespace Codex.Lucene.Framework
{
    public static class Helpers
    {
        public static IEnumerable<int> Enumerate(this DocIdSet docs)
        {
            var iterator = docs.GetIterator();
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
    }
}
