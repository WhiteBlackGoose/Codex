using Codex.ElasticSearch.Store.Scripts;
using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.ElasticSearch
{
    public class ElasticSearchIdRegistry : IStableIdRegistry
    {
        private readonly ElasticSearchStore store;
        private readonly ElasticSearchService service;
        private readonly ElasticSearchEntityStore entityStore;
        private readonly ElasticSearchEntityStore<IStableIdMarker> idStore;
        private readonly IndexIdRegistry[] indexRegistries;

        public ElasticSearchIdRegistry(ElasticSearchStore store)
        {
            this.store = store;
            this.service = store.Service;
            this.idStore = store.StableIdMarkerStore;
            this.indexRegistries = SearchTypes.RegisteredSearchTypes.Select(searchType => new IndexIdRegistry(this, searchType)).ToArray();
            this.entityStore = store.StableIdMarkerStore;
        }

        public Task FinalizeAsync()
        {
            throw new NotImplementedException();
        }

        public Task CommitReservations(IReadOnlyList<string> committedReservations, IReadOnlyList<int> unusedIds = null)
        {
            throw new NotImplementedException();
        }

        public async Task<IStableIdRegistration> SetStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            var registration = new StableIdRegistration();
            foreach (var item in items)
            {
                var indexRegistry = indexRegistries[item.SearchType.Id];
                var nodeAndStableId = await indexRegistry.GetReservationNodeAndStableId(item.StableIdGroup);
                item.StableId = nodeAndStableId.stableId;
                registration.AddReservation(item, nodeAndStableId.node);
            }

            // For each item, reserve ids from the {IndexName}:{StableIdGroup} StableIdMarker document and assign tentative ids for each
            // item
            // Next attempt to get or add a document with Uid = {IndexName}.{Uid}, StableIdGroup, StableId
            // Assign the StableId of the final document after the get or add to the item

            return registration;
        }

        public async Task<IStableIdReservation> ReserveIds(SearchType searchType, int stableIdGroup)
        {
            const int reserveCount = 20;
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
                            .Add("reserveCount", reserveCount))).CaptureRequest(context))
                    .ThrowOnFailure();
            });

            var stableIdMarkerDocument = response.Result.Get.Source;
            return stableIdMarkerDocument.PendingReservations.Where(r => r.ReservationId == reservationId).Single();
        }

        private static string GetStableIdMarkerId(SearchType searchType, int stableIdGroup)
        {
            return $"{searchType.IndexName}#{stableIdGroup}";
        }

        private class StableIdRegistration : IStableIdRegistration
        {
            private readonly ElasticSearchIdRegistry registry;
            private readonly Dictionary<IStableIdItem, ReservationNode> reservations = new Dictionary<IStableIdItem, ReservationNode>();

            public void AddReservation(IStableIdItem item, ReservationNode node)
            {
                reservations.Add(item, node);
            }

            public Task CompleteAsync()
            {
                throw new NotImplementedException();
            }

            public void Report(IStableIdItem item, bool used)
            {
                lock (reservations)
                {
                    var reservationNode = reservations[item];
                    reservations.Remove(item);
                    if (used)
                    {
                        reservationNode.CommitId();
                    }
                    else
                    {
                        reservationNode.ReturnId(item.StableId);
                    }
                }
            }
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

            public ReservationNode CollectCompletedReservationsAndGetNextUncompleted(ref List<string> committedNodes)
            {
                if (committedIdCount == IdReservation.ReservedIds.Count)
                {
                    // Node is committed since all reserved ids are committed
                    if (committedNodes == null)
                    {
                        committedNodes = new List<string>();
                    }

                    committedNodes.Add(this.IdReservation.ReservationId);
                    return Next?.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes);
                }
                else
                {
                    if (Next != null)
                    {
                        Next = Next.CollectCompletedReservationsAndGetNextUncompleted(ref committedNodes);
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

                List<string> completedReservations = null;
                next = next.CollectCompletedReservationsAndGetNextUncompleted(ref completedReservations);
                var node = new ReservationNode(stableIdGroup, reservation, next);

                if (completedReservations != null)
                {
                    await idRegistry.CommitReservations(completedReservations);
                }

                groupReservationNodes[stableIdGroup] = node;
                return node;
            }
        }
    }
}
