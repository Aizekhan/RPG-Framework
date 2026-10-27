using System;
using RPGFramework.Events;

namespace RPGFramework.Housing.Runtime.Events
{
    /// <summary>
    /// Event raised when furniture is removed from a house.
    /// Published by <c>House.TryRemoveFurniture</c> on success (houses built by HousingManager get its publisher).
    /// RemovedBy is Guid.Empty: the house is not told who removed it.
    /// </summary>
    public class FurnitureRemovedEvent : IEvent
    {
        /// <summary>
        /// ID of the house where furniture was removed from.
        /// </summary>
        public Guid HouseId { get; }

        /// <summary>
        /// ID of the removed furniture.
        /// </summary>
        public Guid FurnitureId { get; }

        /// <summary>
        /// Entity that removed the furniture.
        /// </summary>
        public Guid RemovedBy { get; }

        /// <summary>
        /// Timestamp of removal.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a new furniture removed event.
        /// </summary>
        public FurnitureRemovedEvent(
            Guid houseId,
            Guid furnitureId,
            Guid removedBy)
        {
            HouseId = houseId;
            FurnitureId = furnitureId;
            RemovedBy = removedBy;
            Timestamp = DateTime.UtcNow;
        }
    }
}
