using Codex.ElasticSearch.Utilities;
using System.Collections.Generic;
using Codex.Utilities;
using Codex.ElasticSearch.Formats;
using System.Diagnostics;

namespace Codex.ElasticSearch
{
    public class ConcurrentRoaringFilterBuilder
    {
        private const int BatchSize = 10000;

        private readonly BatchQueue<int> Queue = new BatchQueue<int>(BatchSize);
        private object mutex = new object();
        public RoaringDocIdSet RoaringFilter { get; private set; } = RoaringDocIdSet.Empty;

        public ConcurrentRoaringFilterBuilder(RoaringDocIdSet startFilter = null)
        {
            RoaringFilter = startFilter ?? RoaringDocIdSet.Empty;
        }

        public void Add(int id)
        {
            if (id < 0)
            {
                Debug.Fail($"{id}");
            }

            if (Queue.AddAndTryGetBatch(id, out var batch))
            {
                AddIds(batch);
            }
        }

        public void Complete()
        {
            while (Queue.TryGetBatch(out var batch))
            {
                AddIds(batch);
            }
        }

        private void AddIds(List<int> batch)
        {
            lock (mutex)
            {
                batch.Sort();
                var filterBuilder = new RoaringDocIdSet.Builder();

                IEnumerable<int> ids = batch.SortedUnique(Comparer<int>.Default);

                if (RoaringFilter.Count != 0)
                {
                    ids = RoaringFilter.Enumerate().ExclusiveInterleave(ids, Comparer<int>.Default);
                }

                foreach (var id in ids)
                {
                    filterBuilder.Add(id);
                }

                RoaringFilter = filterBuilder.Build();
            }
        }
    }
}
