using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using System.Diagnostics.Contracts;
using Codex.Sdk.Utilities;
using Codex.ElasticSearch.Utilities;
using Codex.Storage.ElasticProviders;
using Codex.ObjectModel;
using System.Net;
using Codex.Logging;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatch
    {
        public readonly BulkDescriptor BulkDescriptor = new BulkDescriptor();
        public static readonly IBulkResponse EmptyResponse = new BulkResponse();

        public readonly List<Item> EntityItems = new List<Item>();
        private readonly List<Item> UncommittedEntityItems = new List<Item>();

        private readonly AtomicBool isReservedForExecute = new AtomicBool();
        private readonly TaskCompletionSource<None> completion = new TaskCompletionSource<None>(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CurrentSize { get; private set; } = 0;
        public int AddedSize { get; private set; } = 0;
        public int Index { get; }

        private readonly ElasticSearchBatcher batcher;
        private readonly IStableIdRegistry stableIdRegistry;
        internal Logger Logger => batcher.store.Configuration.Logger;

        public ElasticSearchBatch(ElasticSearchBatcher batcher, int index)
        {
            this.batcher = batcher;
            this.stableIdRegistry = batcher.StableIdRegistry;
            Index = index;
        }

        public bool TryReserveExecute()
        {
            if (isReservedForExecute.TrySet(value: true))
            {
                lock (this)
                {
                    // Acquire lock to ensure that all additions are finished
                    return true;
                }
            }

            return false;
        }

        public Task Completion => completion.Task;

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            try
            {
                lock (this)
                {
                    // Prevent any subsequent additions by calling this in the lock.
                    // Generally, TryReserveExecute would already be called except
                    // for final batch which may not reach capacity before being flushed
                    TryReserveExecute();
                }

                // Reserve stable ids
                await stableIdRegistry.SetStableIdsAsync(EntityItems);

                foreach (var item in EntityItems)
                {
                    //if (item.SearchType == SearchTypes.BoundSource && item.Entity is IBoundSourceSearchModel boundSource
                    //    && boundSource.BindingInfo.ProjectId == "Domino.Scheduler" && boundSource.BindingInfo.ProjectRelativePath == "DominoScheduler.cs")
                    //{
                    //    System.Diagnostics.Debugger.Launch();
                    //}

                    if (item.IsCommitted)
                    {
                        AddItemToFilters(item);
                    }
                    else
                    {
                        item.AddIndexOperation(BulkDescriptor);
                        UncommittedEntityItems.Add(item);
                    }

                    if (item.SearchType == SearchTypes.Definition)
                    {
                        Logger.LogDiagnosticWithProvenance2($"Uid={item.Uid}, Sid={item.StableId}, Cmt={item.IsCommitted}");
                    }
                }

                if (UncommittedEntityItems.Count == 0)
                {
                    return EmptyResponse;
                }

                var response = await context.Client.BulkAsync(BulkDescriptor.CaptureRequest(context)).ThrowOnFailure(allowInvalid: true);
                Contract.Assert(UncommittedEntityItems.Count == response.Items.Count);

                int batchIndex = 0;
                foreach (var responseItem in response.Items)
                {
                    var item = UncommittedEntityItems[batchIndex];
                    batchIndex++;

                    AddItemToFilters(item);

                    if (item.SearchType == SearchTypes.Definition)
                    {
                        Logger.LogDiagnosticWithProvenance2($"Uid={item.Uid}, Sid={item.StableId}, Cmt={item.IsCommitted}, Rsp={responseItem.Status} [Err={responseItem.Error != null}]");
                    }

                    if (item.SearchType == SearchTypes.TextSource && item.Entity is ITextSourceSearchModel textSource)
                    {
                        Logger.LogDiagnosticWithProvenance($"[Text#{textSource.Uid}] Text({responseItem.Status}|{item.IsAdded}/{item.IsCommitted}|{item.StableId}): {textSource.File.Info.ProjectId}:{textSource.File.Info.ProjectRelativePath}");
                    }
                    else if (item.SearchType == SearchTypes.BoundSource && item.Entity is IBoundSourceSearchModel boundSource)
                    {
                        Logger.LogDiagnosticWithProvenance($"[Bound#{boundSource.Uid}|Text#{boundSource.TextUid}] Bound({responseItem.Status}|{item.IsAdded}/{item.IsCommitted}|{item.StableId}): {boundSource.BindingInfo.ProjectId}:{boundSource.BindingInfo.ProjectRelativePath}");
                    }

                    if (IsAdded(responseItem))
                    {
                        AddedSize += item.Entity.EntityContentSize;
                        item.IsEntityAdded = true;
                    }
                }

                await stableIdRegistry.CommitStableIdsAsync(UncommittedEntityItems);

                return response;
            }
            finally
            {
                completion.SetResult(None.Value);
            }
        }

        private void AddItemToFilters(Item item)
        {
            if (Placeholder.IsCommitModelEnabled)
            {
                var entityRef = new ElasticEntityRef(item.Entity);
                batcher.CommitSearchTypeStoredFilters[item.SearchType.Id].Add(entityRef);
                foreach (var additionalStoredFilter in item.AdditionalStoredFilters)
                {
                    additionalStoredFilter.Add(entityRef);
                }
            }
        }

        private bool IsAdded(IBulkResponseItem item)
        {
            return item.Status == (int)HttpStatusCode.Created;
        }

        public bool TryAdd<T>(ElasticSearchEntityStore<T> store, T entity, ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            if (!CanFit(entity))
            {
                return false;
            }

            lock (this)
            {
                if (!CanFit(entity))
                {
                    return false;
                }

                CurrentSize += entity.EntityContentSize;

                var item = new Item<T>(entity, store)
                {
                    BatchIndex = EntityItems.Count,
                    AdditionalStoredFilters = additionalStoredFilters
                };

                EntityItems.Add(item);
                return true;
            }
        }

        private bool CanFit<T>(T entity) where T : class, ISearchEntity
        {
            var size = CurrentSize;
            if (size != 0 && size + entity.EntityContentSize > ElasticConstants.BatchSizeBytes)
            {
                return false;
            }

            if (isReservedForExecute.Value)
            {
                // Already reserved for execution
                return false;
            }

            return true;
        }

        private class Item<T> : Item
            where T : class, ISearchEntity
        {
            private T entity;
            private ElasticSearchEntityStore<T> store;

            public Item(T entity, ElasticSearchEntityStore<T> store)
            {
                this.entity = entity;
                this.store = store;
            }

            public override ElasticSearchEntityStore EntityStore => store;

            public override ISearchEntity Entity => entity;

            public override void AddIndexOperation(BulkDescriptor bulkDescriptor)
            {
                store.AddIndexOperation(bulkDescriptor, entity);
            }
        }

        internal abstract class Item : IStableIdItem
        {
            public int? StableId { get; set; }
            public int BatchIndex { get; set; }
            public abstract ISearchEntity Entity { get; }
            public SearchType SearchType => EntityStore.SearchType;
            public abstract ElasticSearchEntityStore EntityStore { get; }
            public ElasticSearchStoredFilterBuilder[] AdditionalStoredFilters { get; set; }

            public int StableIdGroup => Entity.RoutingGroup;
            public string Uid => Entity.Uid;

            int IStableIdItem.StableId
            {
                get
                {
                    return StableId.Value;
                }
                set
                {
                    SetStableId(value);
                }
            }

            public bool IsAdded { get; set; }
            public bool IsCommitted { get; set; }
            public bool IsEntityAdded { get; set; }

            public abstract void AddIndexOperation(BulkDescriptor bulkDescriptor);

            public void SetStableId(int stableId)
            {
                StableId = stableId;
                Entity.StableId = stableId;
            }

            public override string ToString()
            {
                return $"SID: {StableId ?? -1}, Group: {StableIdGroup}";
            }
        }
    }
}
