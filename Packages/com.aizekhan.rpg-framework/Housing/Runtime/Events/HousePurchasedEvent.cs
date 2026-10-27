using System;
using RPGFramework.Events;

namespace RPGFramework.Housing.Runtime.Events
{
    /// <summary>
    /// Event raised when a house is purchased.
    /// Published by <c>HousingManager.PurchaseHouse</c> after the plot is taken; Price is the plot's purchase price.
    /// </summary>
    public class HousePurchasedEvent : IEvent
    {
        /// <summary>
        /// ID of the purchased house.
        /// </summary>
        public Guid HouseId { get; }

        /// <summary>
        /// ID of the buyer/owner.
        /// </summary>
        public Guid BuyerId { get; }

        /// <summary>
        /// ID of the plot where the house was built.
        /// </summary>
        public Guid PlotId { get; }

        /// <summary>
        /// Type of house purchased.
        /// </summary>
        public Data.HouseType HouseType { get; }

        /// <summary>
        /// Price paid for the house.
        /// </summary>
        public int Price { get; }

        /// <summary>
        /// Timestamp of purchase.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a new house purchased event.
        /// </summary>
        public HousePurchasedEvent(
            Guid houseId,
            Guid buyerId,
            Guid plotId,
            Data.HouseType houseType,
            int price)
        {
            HouseId = houseId;
            BuyerId = buyerId;
            PlotId = plotId;
            HouseType = houseType;
            Price = price;
            Timestamp = DateTime.UtcNow;
        }
    }
}
