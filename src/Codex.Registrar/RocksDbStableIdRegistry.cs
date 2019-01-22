using Codex.ElasticSearch;
using RocksDbSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Registrar
{
    public class RocksDbStableIdRegistry : IStableIdRegistry
    {
        private RocksDb db;
        private int stableIdCursor;

        private ConcurrentDictionary<string, int> stableIdMapping = new ConcurrentDictionary<string, int>();

        private readonly Func<string, int> getNextStableId;

        public RocksDbStableIdRegistry()
        {
            getNextStableId = GetNextStableId;
        }

        private int GetNextStableId(string arg)
        {
            return Interlocked.Increment(ref stableIdCursor);
        }

        public IReadOnlyList<int> GetStableIds(IReadOnlyList<string> uids)
        {
            int[] results = new int[uids.Count];

            if (!GetStableIdsFromDb(uids, results))
            {
                // Has some missing results

            }

            bool missingResults = GetStableIdsFromDb(uids, results);


            if (!missingResults)
            {
                return results;
            }




            for (int i = 0; i < uids.Count; i++)
            {
                var uid = uids[i];

                var result = db.Get(uid);
                if (!string.IsNullOrEmpty(result))
                {
                    results[i] = int.Parse(result);
                }
                else if (!stableIdMapping.TryGetValue(uid, out var stableId))
                {
                    stableId = stableIdMapping.GetOrAdd(uid, getNextStableId);

                    // Remove the stable id
                    if (!stableIdMapping.TryRemove(uid, out stableId))
                    {
                        result = db.Get(uid);
                    }
                    else
                    {
                        results[i] = stableId;
                    }
                }
            }

            return results;
        }

        public bool GetStableIdsFromDb(IReadOnlyList<string> uids, int[] results)
        {
            bool missingResults = false;
            for (int i = 0; i < uids.Count; i++)
            {
                if (results[i] < 0)
                {
                    continue;
                }

                var uid = uids[i];

                var result = db.Get(uid);
                if (!string.IsNullOrEmpty(result))
                {
                    results[i] = int.Parse(result);
                }
                else
                {
                    missingResults = true;
                    results[i] = -1;
                }
            }

            return !missingResults;
        }

        public Task FinalizeAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetStableIdsAsync(IReadOnlyList<IStableIdItem> uids)
        {
            throw new NotImplementedException();
        }

        public Task CommitStableIds(IReadOnlyList<IStableIdItem> uids)
        {
            throw new NotImplementedException();
        }
    }
}
