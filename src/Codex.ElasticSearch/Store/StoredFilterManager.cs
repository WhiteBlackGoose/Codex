using Codex.ElasticSearch.Formats;
using Codex.ObjectModel;
using Codex.Serialization;
using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Store
{
    public class StoredFilterManager
    {
        public readonly IEntityStore<IStoredFilter> Store;
        private const int HashPathSegmentCount = 3;

        public Task AddStoredFilter(string key, string name, IStoredFilter filter)
        {
            return UpdateStoredFilter(key, name, filter, UpdateMode.Add);
        }

        public Task RemoveStoredFilter(string key, string name, IStoredFilter filter)
        {
            return UpdateStoredFilter(key, name, filter, UpdateMode.Remove);
        }

        public Task ReplaceStoredFilter(string key, string name, IStoredFilter filter)
        {
            return UpdateStoredFilter(key, name, filter, UpdateMode.Replace);
        }

        private enum UpdateMode
        {
            Add = 1,
            Remove = 1 << 1,
            Replace = Add | Remove
        }

        private async Task UpdateStoredFilter(string key, string name, IStoredFilter filter, UpdateMode initialMode)
        {
            string path = GetHashPathFromName(name);
            // Get chain of stored filters leading to name including siblings of each
            // segment in the chain
            var ancestorChain = await GetStoredFilterAncestorChainBottomUp(key, path);

            // For each segment, rewrite to replace child with the filter
            string childFullPath = path;
            IStoredFilter child = filter;
            UpdateMode mode = initialMode;
            foreach (StoredFilter parent in ancestorChain)
            {
                if ((mode & UpdateMode.Remove) == UpdateMode.Remove)
                {
                    if (TryGetChildFilter(parent, childFullPath, out var childReference))
                    {
                        RemoveChild(parent, childReference);
                    }
                }

                if ((mode & UpdateMode.Add) == UpdateMode.Add)
                {
                    AddChild(parent, child, childFullPath);
                }

                // Always need to replace going up the parent chain after initial modification
                mode = UpdateMode.Replace;

                RecomputeFilter(parent);

                child = parent;
                childFullPath = parent.FullPath;
            }

            // Update key with new stored filter
            await Update(key, ancestorChain);
        }

        public static string GetHashPathFromName(string name)
        {
            name = name.ToLowerInvariant();
            var hash = IndexingUtilities.ComputeFullHash(name);
            var path = Path.Combine(Enumerable.Range(1, HashPathSegmentCount).Select(i => i == HashPathSegmentCount ? name : hash.GetByte(i).ToString("X")).ToArray());
            return path;
        }

        public void RecomputeFilter(StoredFilter filter)
        {
            var filterBuilder = new RoaringDocIdSet.Builder();

            foreach (var id in CombineStableIds(0, filter.Children.Count, filter.Children))
            {
                filterBuilder.Add(id);
            }

            var stableIds = filterBuilder.Build();
            filter.StableIds = stableIds.GetBytes();
            filter.Cardinality = stableIds.Cardinality();

            filter.PopulateContentIdAndSize(force: true);
        }

        public IEnumerable<int> CombineStableIds(int start, int length, IReadOnlyList<IChildFilterReference> children)
        {
            var halfLength = length / 2;
            switch (length)
            {
                case 0:
                    return Enumerable.Empty<int>();
                case 1:
                    return RoaringDocIdSet.FromBytes(children[start].ChildStableIds).Enumerate();
                default:
                    return CollectionUtilities.ExclusiveInterleave(
                        CombineStableIds(start, halfLength, children),
                        CombineStableIds(start + halfLength, length - halfLength, children), 
                        Comparer<int>.Default);
            }
        }

        public void RemoveChild(StoredFilter parent, ChildFilterReference child)
        {
            parent.Children.Remove(child);
        }

        public void AddChild(StoredFilter parent, IStoredFilter child, string childFullPath)
        {
            parent.Children.Add(new ChildFilterReference()
            {
                ChildStableIds = child.StableIds,
                ChildUid = child.Uid,
                FullPath = childFullPath
            });
        }

        public bool TryGetChildFilter(StoredFilter filter, string fullPath, out ChildFilterReference childFilter)
        {
            foreach (var child in filter.Children)
            {
                if (child.FullPath == fullPath)
                {
                    childFilter = child;
                    return true;
                }
            }

            childFilter = null;
            return false;
        }

        public Task Update(string key, StoredFilter[] updatedChain)
        {
            var rootFilter = updatedChain.Last();
            rootFilter.Uid = key;

            throw new NotImplementedException();
        }

        public async Task<StoredFilter[]> GetStoredFilterAncestorChainBottomUp(string key, string path)
        {
            StoredFilter[] filters = new StoredFilter[HashPathSegmentCount + 1];

            var uid = key;
            string currentPath = null;
            for (int i = 0; i <= HashPathSegmentCount; i++)
            {
                var result = await Store.GetAsync(new[] { uid });
                var filter = (StoredFilter)result.FirstOrDefault() ?? new StoredFilter()
                {
                    FullPath = currentPath,
                };

                filters[i] = filter;

                currentPath = path.Substring(0, Math.Min(path.Length, 1 + i * 2));
            }

            throw new NotImplementedException();
        }
    }
}
