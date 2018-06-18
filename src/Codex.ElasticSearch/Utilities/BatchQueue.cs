using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.ElasticSearch.Utilities
{
    public class BatchQueue<T>
    {
        private int totalCount;
        private int batchSize;
        private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

        public BatchQueue(int batchSize)
        {
            this.batchSize = batchSize;
        }

        public int TotalCount => totalCount;

        public bool AddAndTryGetBatch(T item, out IReadOnlyList<T> batch)
        {
            Placeholder.Todo("Get final batch");
            queue.Enqueue(item);
            var updatedTotalCount = Interlocked.Increment(ref totalCount);
            if (updatedTotalCount % batchSize == 0)
            {
                List<T> batchList = new List<T>(batchSize);
                for (int i = 0; i < batchSize; i++)
                {
                    T dequeuedItem;
                    if (queue.TryDequeue(out dequeuedItem))
                    {
                        batchList.Add(dequeuedItem);
                    }
                    else
                    {
                        break;
                    }
                }

                if (batchList.Count != 0)
                {
                    batch = batchList;
                    return true;
                }
            }

            batch = null;
            return false;
        }
    }
}
