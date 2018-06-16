// This defines an ElasticSearch Painless script for 'committing' a pending reservation which
// adds unused reserved stable ids to the free list and removes the pending reservation
// CommitReservation(string[] reservationIds, int[] unusedIds)

// Parameters
List unusedIds = params.returnedIds;
List reservationIds = params.reservationIds;

// Add all unused ids to the free list
ctx._source.freeList.addAll(unusedIds);

for (String reservationId : reservationIds)
{
	// Remove the reservation id
	ctx._source.pendingReservations.removeIf(item -> item.reservationId == reservationId);
}