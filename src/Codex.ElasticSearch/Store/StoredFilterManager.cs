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

        public StoredFilterManager(IEntityStore<IStoredFilter> store)
        {
            Store = store;
        }

        public Task<IStoredFilter> AddStoredFilterAsync(string key, string name, IStoredFilter filter)
        {
            return UpdateStoredFilter(key, name, filter, UpdateMode.Replace);
        }

        public Task<IStoredFilter> RemoveStoredFilterAsync(string key, string name)
        {
            IStoredFilter filter = null;
            return UpdateStoredFilter(key, name, filter, UpdateMode.Remove);
        }

        private enum UpdateMode
        {
            Add = 1,
            Remove = 1 << 1,
            Replace = Add | Remove
        }

        private async Task<IStoredFilter> UpdateStoredFilter(string key, string name, IStoredFilter filter, UpdateMode initialMode)
        {
            string path = GetHashPathFromName(name);
            // Get chain of stored filters leading to name including siblings of each
            // segment in the chain
            var ancestorChain = await GetStoredFilterAncestorChainBottomUp(key, path, createIfMissing: initialMode != UpdateMode.Remove);
            if (ancestorChain == null)
            {
                return null;
            }

            // For each segment, rewrite to replace child with the filter
            string childFullPath = path;
            IStoredFilter child = filter;
            UpdateMode mode = initialMode;
            List<StoredFilter> modifiedAncestorChain = new List<StoredFilter>();
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

                RecomputeFilter(parent, modifiedAncestorChain);

                child = parent;
                childFullPath = parent.FullPath;
            }

            // Update key with new stored filter
            return await Update(key, modifiedAncestorChain);
        }

        public static string GetHashPathFromName(string name)
        {
            name = name.ToLowerInvariant();
            var hash = IndexingUtilities.ComputeFullHash(name);
            var path = Path.Combine(Enumerable.Range(1, HashPathSegmentCount).Select(i => i == HashPathSegmentCount ? name : hash.GetByte(i).ToString("X").ToLowerInvariant()).ToArray());
            return path;
        }

        public void RecomputeFilter(StoredFilter filter, List<StoredFilter> modifiedStoredFilters)
        {
            var priorUid = filter.Uid;
            var priorContentId = filter.EntityContentId;

            filter.ApplyStableIds(CombineStableIds(0, filter.Children.Count, filter.Children));
            filter.PopulateContentIdAndSize(force: true);

            if (priorUid != filter.Uid || priorContentId != filter.EntityContentId)
            {
                modifiedStoredFilters.Add(filter);
            }
        }

        public IEnumerable<int> CombineStableIds(int start, int length, IReadOnlyList<IChildFilterReference> children)
        {
            var halfLength = length / 2;
            switch (length)
            {
                case 0:
                    return Enumerable.Empty<int>();
                case 1:
                    return RoaringDocIdSet.FromBytes(children[start].StableIds).Enumerate();
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
                Cardinality = child.Cardinality,
                StableIds = child.StableIds,
                Uid = child.Uid,
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

        public async Task<IStoredFilter> Update(string key, List<StoredFilter> updatedChain)
        {
            var rootFilter = updatedChain.Last();
            rootFilter.Uid = key;

            await Store.StoreAsync(updatedChain);

            return rootFilter;
        }

        public async Task<StoredFilter[]> GetStoredFilterAncestorChainBottomUp(string key, string path, bool createIfMissing)
        {
            StoredFilter[] filters = new StoredFilter[HashPathSegmentCount];

            var uid = key;
            string currentPath = null;
            for (int i = 0; i < HashPathSegmentCount; i++)
            {
                var result = uid != null ? await Store.GetAsync(new[] { uid }) : CollectionUtilities.Empty<StoredFilter>.Array;
                var retrievedFilter = (StoredFilter)result.FirstOrDefault();
                var filter = retrievedFilter ?? new StoredFilter()
                {
                    FullPath = currentPath,
                };

                if (retrievedFilter == null && !createIfMissing)
                {
                    return null;
                }

                filters[i] = filter;
                
                currentPath = path.Substring(0, Math.Min(path.Length, (i * 3) + 2 /* two hex chars and a slash */));
                if (retrievedFilter != null && TryGetChildFilter(retrievedFilter, currentPath, out var childRef))
                {
                    uid = childRef.Uid;
                }
                else
                {
                    uid = null;
                }
            }

            Array.Reverse(filters);
            return filters;
        }
    }
}
