using Codex.ElasticSearch.Store.Scripts;
using Codex.ElasticSearch.Utilities;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Nest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
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
        private readonly IndexIdRegistry[] indexRegistries;

        public ElasticSearchIdRegistry(ElasticSearchStore store)
        {
            this.store = store;
            this.service = store.Service;
            this.idStore = store.StableIdMarkerStore;
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

        public async Task<IStableIdRegistration> SetStableIdsAsync(IReadOnlyList<IStableIdItem> items)
        {
            var registration = new StableIdRegistration(this);
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

        private class StableIdRegistration : IStableIdRegistration
        {
            private readonly ElasticSearchIdRegistry registry;
            private readonly Dictionary<IStableIdItem, ReservationNode> reservations = new Dictionary<IStableIdItem, ReservationNode>();

            public StableIdRegistration(ElasticSearchIdRegistry registry)
            {
                this.registry = registry;
            }

            public void AddReservation(IStableIdItem item, ReservationNode node)
            {
                reservations.Add(item, node);
            }

            public void Dispose()
            {
                Contract.Assert(reservations.Count == 0);
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
