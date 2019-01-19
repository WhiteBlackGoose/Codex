﻿using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Utilities;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Codex.ObjectModel;
using System.Collections.Generic;
using Codex.ElasticSearch.Store;
using static Codex.ElasticSearch.StoredFilterUtilities;
using Codex.Storage.ElasticProviders;

namespace Codex.ElasticSearch
{
    internal class ElasticSearchBatcher
    {
        /// <summary>
        /// Defines stored filters for each entity type
        /// </summary>
        public readonly ElasticSearchStoredFilterBuilder[] CommitSearchTypeStoredFilters = new ElasticSearchStoredFilterBuilder[SearchTypes.RegisteredSearchTypes.Count];

        /// <summary>
        /// Defines stored filter for declared definitions in the current commit
        /// </summary>
        /// <remarks>
        /// This is an array because Add takes an array not a single item
        /// </remarks>
        public readonly ElasticSearchStoredFilterBuilder[] DeclaredDefinitionStoredFilter;

        /// <summary>
        /// Empty set of stored filters for passing as additional stored filters
        /// </summary>
        internal readonly ElasticSearchStoredFilterBuilder[] EmptyStoredFilters = Array.Empty<ElasticSearchStoredFilterBuilder>();

        private readonly ConcurrentQueue<ValueTask<None>> backgroundTasks = new ConcurrentQueue<ValueTask<None>>();

        internal IStableIdRegistry StableIdRegistry { get; }
        private readonly ElasticSearchService service;
        internal readonly ElasticSearchStore store;

        public long BatchIndex;
        public long TotalSize;
        public long TotalAddedSize;

        private ElasticSearchBatch[] batches;
        private int batchCounter = 0;
        private AtomicBool backgroundDequeueReservation = new AtomicBool();

        public ElasticSearchBatcher(ElasticSearchStore store, IStableIdRegistry stableIdRegistry, string commitFilterName, string repositoryFilterName, string cumulativeCommitFilterName)
        {
            Debug.Assert(store.Initialized, "Store must be initialized");

            this.store = store;
            this.StableIdRegistry = stableIdRegistry;
            service = store.Service;
            batches = Enumerable.Range(0, store.Configuration.MaxBatchConcurrency).Select(i => new ElasticSearchBatch(this, i)).ToArray();

            CommitSearchTypeStoredFilters = store.EntityStores.Select(entityStore =>
            {
                return new ElasticSearchStoredFilterBuilder(
                    entityStore,
                    filterName: commitFilterName,
                    unionFilterNames: new[] { repositoryFilterName, cumulativeCommitFilterName });
            }).ToArray();

            var declaredDefinitionStoredFilter = new ElasticSearchStoredFilterBuilder(
                    store.DefinitionStore,
                    filterName: commitFilterName);

            declaredDefinitionStoredFilter.IndexName = GetDeclaredDefinitionsIndexName(declaredDefinitionStoredFilter.IndexName);
            DeclaredDefinitionStoredFilter = new[] { declaredDefinitionStoredFilter };
        }

        public async ValueTask<None> AddAsync<T>(ElasticSearchEntityStore<T> store, T entity, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            // This part must be synchronous since Add calls this method and
            // assumes that the content id and size will be set
            PopulateContentIdAndSize(entity, store);
            var index = Interlocked.Increment(ref batchCounter) % batches.Length;

            while (true)
            {
                var batch = batches[index];
                if (!batch.TryAdd(store, entity, additionalStoredFilters))
                {
                    if (batch.TryReserveExecute())
                    {
                        await ExecuteBatchAsync(batch);
                        return None.Value;
                    }
                    else
                    {
                        await batch.Completion;
                        Interlocked.CompareExchange(ref batches[index], new ElasticSearchBatch(this, index), batch);
                    }
                }
                else
                {
                    // Added to batch so batch is not full. Don't need to execute batch yet so return.
                    return None.Value;
                }
            }
        }

        private async Task ExecuteBatchAsync(ElasticSearchBatch batch)
        {
            await service.UseClient(batch.ExecuteAsync);
            Console.WriteLine($"Sent batch ({Interlocked.Increment(ref BatchIndex)}#{batch.Index}): Size={batch.CurrentSize}, AddedSize={batch.AddedSize}, TotalSize={TotalSize}, TotalAddedSize={TotalAddedSize}");

            Interlocked.Add(ref TotalSize, batch.CurrentSize);
            Interlocked.Add(ref TotalAddedSize, batch.AddedSize);

            if (backgroundDequeueReservation.TrySet(true))
            {
                while (backgroundTasks.TryPeek(out var dequeuedTask))
                {
                    if (dequeuedTask.IsCompleted)
                    {
                        backgroundTasks.TryDequeue(out dequeuedTask);
                    }
                    else
                    {
                        break;
                    }
                }

                backgroundDequeueReservation.TrySet(false);
            }
        }

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity, params ElasticSearchStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity
        {
            var backgroundTask = AddAsync(store, entity, additionalStoredFilters: additionalStoredFilters);
            if (!backgroundTask.IsCompleted)
            {
                backgroundTasks.Enqueue(backgroundTask);
            }
        }

        public void PopulateContentIdAndSize<T>(T entity, ElasticSearchEntityStore<T> store)
            where T : class, ISearchEntity
        {
            if (entity.RoutingKey == null && store.EntitySearchType.GetRoutingKey != null)
            {
                entity.RoutingKey = store.EntitySearchType.GetRoutingKey(entity);
            }

            entity.PopulateContentIdAndSize();
        }

        public async Task FinalizeAsync(string repositoryName)
        {
            while (true)
            {
                // Flush any background operations
                while (backgroundTasks.TryDequeue(out var backgroundTask))
                {
                    await backgroundTask;
                }

                int beforeBatchCounter = batchCounter;
                foreach (var batch in batches)
                {
                    if (batch.EntityItems.Count == 0)
                    {
                        continue;
                    }

                    if (batch.TryReserveExecute())
                    {
                        await ExecuteBatchAsync(batch);
                    }
                    else
                    {
                        await batch.Completion;
                    }
                }

                if (batchCounter == beforeBatchCounter && backgroundTasks.IsEmpty)
                {
                    break;
                }
            }

            Console.WriteLine($"Finished processing batches: TotalSize={TotalSize}, TotalAddedSize={TotalAddedSize}");

            // For each typed stored filter,
            // Store the filter under 

            StoredFilterManager filterManager = new StoredFilterManager(store.StoredFilterStore);
            var combinedSourcesFilterName = store.Configuration.CombinedSourcesFilterName;

            var ingestId = Guid.NewGuid();
            string repositorySnapshotId = GetRepositoryBaseFilterName($"{repositoryName}/{ingestId}");
            string combinedSourcesSnapshotId = GetRepositoryBaseFilterName($"{store.Configuration.CombinedSourcesFilterName}/{ingestId}");

            Console.WriteLine($"Writing repository snapshot filters: {repositorySnapshotId}");

            // Finalize the stored filters
            foreach (var filterBuilder in CommitSearchTypeStoredFilters.Concat(DeclaredDefinitionStoredFilter))
            {
                var filter = await filterBuilder.FinalizeAsync();

                var combinedSourceFilter = await filterManager.AddStoredFilterAsync(
                    key: GetFilterName(combinedSourcesFilterName, indexName: filterBuilder.IndexName),
                    name: repositoryName,
                    filter: filter);

                // repos/{repositoryName}/{ingestId}:{indexName}
                filter.Uid = GetIndexRepositoryFilterUid(repositorySnapshotId, filterBuilder.IndexName);
                combinedSourceFilter = new StoredFilter(combinedSourceFilter)
                {
                    // repos/allsources/{ingestId}:{indexName}
                    Uid = GetIndexRepositoryFilterUid(combinedSourcesSnapshotId, filterBuilder.IndexName)
                };

                await store.StoredFilterStore.StoreAsync(new[] { filter, combinedSourceFilter });
            }

            Console.WriteLine($"Writing repository snapshot: {repositorySnapshotId}");

            await store.PropertyStore.StoreAsync(new[] { new PropertySearchModel()
                {
                    // aliases/repos/{repositoryName}
                    Uid = GetStoredFilterAliasUid(repositoryName),
                    Key = "RepositorySnapshotId",

                    // repos/{repositoryName}/{ingestId}
                    Value = repositorySnapshotId,
                },
                new PropertySearchModel()
                {
                    // aliases/repos/allsources
                    Uid = GetStoredFilterAliasUid(store.Configuration.CombinedSourcesFilterName),
                    Key = "CombinedSourcesSnapshotId",
                    // repos/allsources/{ingestId}
                    Value = combinedSourcesSnapshotId,
                }
            });

            await RefreshStoredFilterIndex();
        }

        private async Task RefreshStoredFilterIndex()
        {
            await service.UseClient(async context =>
            {
                return await context.Client.RefreshAsync(store.StoredFilterStore.IndexName).ThrowOnFailure();
            });
        }
    }
}
