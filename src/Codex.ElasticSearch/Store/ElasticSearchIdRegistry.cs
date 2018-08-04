using Codex.ElasticSearch.Store.Scripts;
using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public class ElasticSearchIdRegistry : IStableIdRegistry
    {
        public const int ReserveCount = 20;

        private readonly ElasticSearchStore store;
        private readonly ElasticSearchService service;
        private readonly ElasticSearchEntityStore entityStore;
        private readonly ElasticSearchEntityStore<IStableIdMarker> idStore;
        private readonly ElasticSearchEntityStore<IRegisteredEntity> registryStore;
        private readonly IndexIdRegistry[] indexRegistries;

        public ElasticSearchIdRegistry(ElasticSearchStore store)
        {
            this.store = store;
            this.service = store.Service;
            this.idStore = store.StableIdMarkerStore;
            this.registryStore = store.RegisteredEntityStore;
            this.indexRegistries = SearchTypes.RegisteredSearchTypes.Select(searchType => new IndexIdRegistry(this, searchType)).ToArray();
            this.entityStore = store.StableIdMarkerStore;
        }

        public async Task FinalizeAsync()
        {
            foreach (var indexRegistry in indexRegistries)
            {
                await indexRegistry.FinalizeAsync();
            }
        }

        public async Task CompleteReservations(
            SearchType searchType, 
            int stableIdGroup,
            IReadOnlyList<string> completedReservations, 
            IReadOnlyList<int> unusedIds = null)
        {
            completedReservations = completedReservations ?? CollectionUtilities.Empty<string>.Array;
            unusedIds = unusedIds ?? CollectionUtilities.Empty<int>.Array;

            var response = await this.service.UseClient(context =>
            {
                var client = context.Client;
                string stableIdMarkerId = GetStableIdMarkerId(searchType, stableIdGroup);
                return client.UpdateAsync<IStableIdMarker, IStableIdMarker>(stableIdMarkerId,
                    ud => ud
                    .Index(entityStore.IndexName)
                    .Upsert(new StableIdMarker())
                    .ScriptedUpsert()
                    .Source()
                    .RetryOnConflict(10)
                    .Script(sd => sd
                        .Source(Scripts.CommitReservation)
                        .Lang(ScriptLang.Painless)
                        .Params(pd => pd
                            .Add("reservationIds", completedReservations)
                            .Add("returnedIds", unusedIds))).CaptureRequest(context))
                    .ThrowOnFailure();
            });

            var stableIdMarkerDocument = response.Result.Get.Source;
        }

        public async Task SetStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            // For each item, reserve ids from the {IndexName}:{StableIdGroup} StableIdMarker document and assign tentative ids for each
            // item
            // Next attempt to get or add a document with Uid = {IndexName}.{Uid}, StableIdGroup, StableId
            // Assign the StableId of the final document after the get or add to the item

            var registeredEntities = new RegisteredEntity[items.Count];
            var reservations = new (ReservationNode node, int stableId)[items.Count];

            var bd = new BulkDescriptor();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var indexRegistry = indexRegistries[item.SearchType.Id];
                var nodeAndStableId = await indexRegistry.GetReservationNodeAndStableId(item.StableIdGroup);
                reservations[i] = nodeAndStableId;

                var version = StoredFilterUtilities.ComputeVersion(item.StableIdGroup, nodeAndStableId.stableId);

                var registeredEntity = new RegisteredEntity()
                {
                    Uid = GetRegistrationUid(item.SearchType, item.Uid),
                    StableId = nodeAndStableId.stableId,
                    StableIdGroup = item.StableIdGroup,
                    DateAdded = DateTime.UtcNow,
                    EntityVersion = version,
                };

                registeredEntities[i] = registeredEntity;
                registryStore.AddIndexOperation(bd, registeredEntity);
            }

            var registerResponse = await store.Service.UseClient(context => context.Client.BulkAsync(bd.CaptureRequest(context))
                    .ThrowOnFailure(allowInvalid: true));

            int index = 0;
            foreach (var registerResponseItem in registerResponse.Result.Items)
            {
                var item = items[index];
                var nodeAndStableId = reservations[index];
                var node = nodeAndStableId.node;
                var stableId = nodeAndStableId.stableId;

                bool added = IsAdded(registerResponseItem);
                item.IsAdded = added;
                if (added)
                {
                    item.StableId = stableId;
                    node.CommitId();
                }
                else
                {
                    item.StableId = StoredFilterUtilities.ExtractStableId(registerResponseItem.Version);
                    node.ReturnId(stableId);
                }

                index++;
            }
        }

        private bool IsAdded(IBulkResponseItem item)
        {
            return item.Status == (int)HttpStatusCode.Created;
        }

        public async Task<IStableIdReservation> ReserveIds(SearchType searchType, int stableIdGroup)
        {
            string reservationId = Guid.NewGuid().ToString();
            var response = await this.service.UseClient(context =>
            {
                var client = context.Client;
                string stableIdMarkerId = GetStableIdMarkerId(searchType, stableIdGroup);
                return client.UpdateAsync<IStableIdMarker, IStableIdMarker>(stableIdMarkerId, 
                    ud => ud
                    .Index(entityStore.IndexName)
                    .Upsert(new StableIdMarker())
                    .ScriptedUpsert()
                    .Source()
                    .RetryOnConflict(10)
                    .Script(sd => sd
                        .Source(Scripts.Reserve)
                        .Lang(ScriptLang.Painless)
                        .Params(pd => pd
                            .Add("reservationId", reservationId)
                            .Add("reserveCount", ReserveCount))).CaptureRequest(context))
                    .ThrowOnFailure();
            });

            var stableIdMarkerDocument = response.Result.Get.Source;
            return stableIdMarkerDocument.PendingReservations.Where(r => r.ReservationId == reservationId).Single();
        }

        private static string GetStableIdMarkerId(SearchType searchType, int stableIdGroup)
        {
            return $"{searchType.IndexName}#{stableIdGroup}";
        }

        private static string GetRegistrationUid(SearchType searchType, string entityUid)
        {
            return $"{searchType.IndexName}:{entityUid}";
        }

        private class ReservationNode
        {
            public readonly int StableIdGroup;
            public readonly IStableIdReservation IdReservation;
            private readonly HashSet<int> remainingIds;
            private int committedIdCount;
            public ReservationNode Next;

            public ReservationNode(int stableIdGroup, IStableIdReservation idReservation, ReservationNode next)
            {
                StableIdGroup = stableIdGroup;
                IdReservation = idReservation;
                Next = next;
                remainingIds = new HashSet<int>(idReservation.ReservedIds);
            }

            public bool TryTakeId(out ReservationNode node, out int id)
            {
                lock (remainingIds)
                {
                    foreach (var remainingId in remainingIds)
                    {
                        id = remainingId;
                        node = this;
                        remainingIds.Remove(remainingId);
                        return true;
                    }
                }

                var next = Next;
                if (next == null)
                {
                    id = 0;
                    node = null;
                    return false;
                }
                else
                {
                    return next.TryTakeId(out node, out id);
                }
            }

            public void ReturnId(int id)
            {
                lock (remainingIds)
                {
                    remainingIds.Add(id);
                }
            }

            public void CommitId()
            {
                Interlocked.Increment(ref committedIdCount);
            }

            public ReservationNode CollectCompletedReservationsAndGetNextUncompleted(ref List<string> committedNodes, List<int> finalizationUnusedIds)
            {
                bool isFinalizing = finalizationUnusedIds != null;
                if (committedIdCount == IdReservation.ReservedIds.Count || isFinalizing)
                {
                    // Node is committed since all reserved ids are committed
                    if (committedNodes == null)
                    {
                        committedNodes = new List<string>();
                    }

                    finalizationUnusedIds?.AddRange(remainingIds);
                    committedNodes.Add(this.IdReservation.ReservationId);
                    return Next?.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes, finalizationUnusedIds);
                }
                else
                {
                    if (Next != null)
                    {
                        Next = Next.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes, finalizationUnusedIds);
                    }

                    return this;
                }
            }
        }

        private class IndexIdRegistry
        {
            private readonly ElasticSearchIdRegistry idRegistry;
            public readonly SearchType SearchType;
            private readonly ReservationNode[] groupReservationNodes;
            private readonly ConcurrentDictionary<int, Box<Lazy<Task<ReservationNode>>>> reservationTasks = new ConcurrentDictionary<int, Box<Lazy<Task<ReservationNode>>>>();

            public IndexIdRegistry(ElasticSearchIdRegistry idRegistry, SearchType searchType)
            {
                this.idRegistry = idRegistry;
                this.SearchType = searchType;
                this.groupReservationNodes = new ReservationNode[StoredFilterUtilities.StableIdGroupMaxValue];
            }

            public async ValueTask<(ReservationNode node, int stableId)> GetReservationNodeAndStableId(int stableIdGroup)
            {
                var node = groupReservationNodes[stableIdGroup];
                if (node != null)
                {
                    if (node.TryTakeId(out var reservedNode, out var stableId))
                    {
                        return (reservedNode, stableId);
                    }
                }

                var lazyReservationNodeBox = reservationTasks.GetOrAdd(stableIdGroup, new Lazy<Task<ReservationNode>>(() => CreateReservationNode(stableIdGroup)));
                var lazyReservation = lazyReservationNodeBox.Value;

                while (true)
                {
                    node = await lazyReservation.Value;
                    if (node.TryTakeId(out var reservedNode, out var stableId))
                    {
                        return (reservedNode, stableId);
                    }
                    else
                    {
                        lazyReservation = Interlocked.CompareExchange(ref lazyReservationNodeBox.Value,
                            new Lazy<Task<ReservationNode>>(() => CreateReservationNode(stableIdGroup, next: node)),
                            lazyReservation);
                    }
                }

                throw new Exception("Unreachable");
            }

            private async Task<ReservationNode> CreateReservationNode(int stableIdGroup, ReservationNode next = null)
            {
                var reservation = await idRegistry.ReserveIds(SearchType, stableIdGroup);

                next = await CompletePendingReservations(stableIdGroup, next, finalizationUnusedIds: null);

                var node = new ReservationNode(stableIdGroup, reservation, next);
                groupReservationNodes[stableIdGroup] = node;
                return node;
            }

            private async Task<ReservationNode> CompletePendingReservations(int stableIdGroup, ReservationNode node, List<int> finalizationUnusedIds)
            {
                List<string> completedReservations = null;
                node = node?.CollectCompletedReservationsAndGetNextUncompleted(ref completedReservations, finalizationUnusedIds: finalizationUnusedIds);
                if (completedReservations != null)
                {
                    await idRegistry.CompleteReservations(SearchType, stableIdGroup, completedReservations, unusedIds: finalizationUnusedIds);
                }

                return node;
            }

            public async Task FinalizeAsync()
            {
                for (int stableIdGroup = 0; stableIdGroup < groupReservationNodes.Length; stableIdGroup++)
                {
                    var reservationNode = groupReservationNodes[stableIdGroup];
                    if (reservationNode != null)
                    {
                        await CompletePendingReservations(stableIdGroup, reservationNode, finalizationUnusedIds: new List<int>());
                    }
                }
            }
        }
    }
}
