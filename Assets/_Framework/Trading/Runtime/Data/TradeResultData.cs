using System.Collections.Generic;

namespace RPGFramework.Trading
{
    /// <summary>
    /// Result of a trade operation.
    /// Contains items received and gold change.
    /// </summary>
    [System.Serializable]
    public struct TradeResultData
    {
        /// <summary>Whether the trade was successful</summary>
        public bool Success;

        /// <summary>Error message if trade failed</summary>
        public string ErrorMessage;

        /// <summary>Items player received in the trade</summary>
        public List<TradeItem> ItemsReceived;

        /// <summary>Items player gave in the trade</summary>
        public List<TradeItem> ItemsGiven;

        /// <summary>Gold change (positive = gain, negative = spent)</summary>
        public long GoldChange;

        /// <summary>ID of the trade that was executed</summary>
        public string TradeId;

        /// <summary>Timestamp of the trade</summary>
        public float Timestamp;

        public TradeResultData(bool success, string errorMessage = "")
        {
            Success = success;
            ErrorMessage = errorMessage;
            ItemsReceived = new List<TradeItem>();
            ItemsGiven = new List<TradeItem>();
            GoldChange = 0;
            TradeId = "";
            Timestamp = 0f;
        }

        /// <summary>
        /// Creates a successful trade result.
        /// </summary>
        public static TradeResultData CreateSuccess(string tradeId, long goldChange, float timestamp)
        {
            return new TradeResultData(true)
            {
                TradeId = tradeId,
                GoldChange = goldChange,
                Timestamp = timestamp
            };
        }

        /// <summary>
        /// Creates a failed trade result with error message.
        /// </summary>
        public static TradeResultData CreateFailure(string errorMessage)
        {
            return new TradeResultData(false, errorMessage);
        }
    }
}
