// This defines an ElasticSearch Painless script for reserving stable ids from a document
// Reserve(string reservationId, int reserveCount)

// Parameters
int reserveCount = params.reserveCount;
String reservationId = params.reservationId;

def sourceDoc = ctx._source;
if (ctx.op == "create")
{
	sourceDoc.nextValue = 0;
	sourceDoc.freeList = new ArrayList();
	sourceDoc.pendingReservations = new ArrayList();
}

int nextValue = sourceDoc.nextValue;
List freeList = sourceDoc.freeList;
List reservedIds = new ArrayList();

// Remove entries from free list
while (reserveCount > 0 && freeList.size() > 0)
{
    int lastIndex = freeList.size() - 1;
    reservedIds.add(freeList.get(lastIndex));
    freeList.remove(lastIndex);
    reserveCount--;
}

// Reserve remaining ids by incrementing nextValue field
while (reserveCount > 0)
{
    reservedIds.add(nextValue);
    nextValue++;
    reserveCount--;
}

sourceDoc.nextValue = nextValue;

if (sourceDoc.pendingReservations == null) {
    sourceDoc.pendingReservations = new ArrayList();
}

// Add the pending reservation with the reserved ids
sourceDoc.pendingReservations.add([
    "reservationId": reservationId,
    "reservationDate": new Date(),
    "reservedIds": reservedIds
]);