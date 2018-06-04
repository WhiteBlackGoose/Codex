using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    /// <summary>
    /// Marker document for tracking reservation of stable ids from a given stable id shard
    /// </summary>
    public interface IStableIdMarker : ISearchEntity
    {
        /// <summary>
        /// The next tail available stable id
        /// </summary>
        int NextValue { get; }

        /// <summary>
        /// The list of free indices
        /// </summary>
        IReadOnlyList<int> FreeList { get; }

        /// <summary>
        /// The uncommitted reservations
        /// </summary>
        IReadOnlyList<IStableIdReservation> PendingReservations { get; }
    }

    /// <summary>
    /// A reservation of a set of stable ids.
    /// </summary>
    public interface IStableIdReservation
    {
        /// <summary>
        /// Unique id of the reservation. Used during commit to find and remove the reservation.
        /// </summary>
        string ReservationId { get; }

        /// <summary>
        /// The date of the id reservation. Used for garbage collection of stale reservations.
        /// </summary>
        DateTime ReservationDate { get; }

        /// <summary>
        /// The list of reserved ids for the reservation
        /// </summary>
        IReadOnlyList<int> ReservedIds { get; }
    }

}
