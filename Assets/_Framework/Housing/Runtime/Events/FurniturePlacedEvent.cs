using System;

namespace RPGFramework.Housing.Runtime.Events
{
    /// <summary>
    /// Event raised when furniture is placed in a house.
    /// </summary>
    public class FurniturePlacedEvent
    {
        /// <summary>
        /// ID of the house where furniture was placed.
        /// </summary>
        public Guid HouseId { get; }

        /// <summary>
        /// ID of the placed furniture.
        /// </summary>
        public Guid FurnitureId { get; }

        /// <summary>
        /// Template ID of the furniture.
        /// </summary>
        public string TemplateId { get; }

        /// <summary>
        /// Category of furniture.
        /// </summary>
        public Data.FurnitureCategory Category { get; }

        /// <summary>
        /// Entity that placed the furniture.
        /// </summary>
        public Guid PlacedBy { get; }

        /// <summary>
        /// Position where furniture was placed.
        /// </summary>
        public (float X, float Y, float Z) Position { get; }

        /// <summary>
        /// Timestamp of placement.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a new furniture placed event.
        /// </summary>
        public FurniturePlacedEvent(
            Guid houseId,
            Guid furnitureId,
            string templateId,
            Data.FurnitureCategory category,
            Guid placedBy,
            float x, float y, float z)
        {
            HouseId = houseId;
            FurnitureId = furnitureId;
            TemplateId = templateId;
            Category = category;
            PlacedBy = placedBy;
            Position = (x, y, z);
            Timestamp = DateTime.UtcNow;
        }
    }
}
