using System.Text.Json.Serialization;

namespace LedgerApi.Domain
{
    /// <summary>
    /// Enumerates the types of ledger entries, which determine how the transaction amount affects the account balance.
    /// </summary>
    [JsonConverter(typeof(StringOnlyEnumConverter))]
    public enum TransactionType
    {
        /// <summary>Money received. Increases the balance.</summary>
        Charge,

        /// <summary>Returns part or all of a prior <see cref="Charge"/>. Decreases the balance; requires an original transaction and cannot exceed it.</summary>
        Refund,

        /// <summary>Money paid out of the account. Decreases the balance; cannot exceed it.</summary>
        Payout,
    }

    /// <summary>
    /// Serializes enum values as their names; rejects integer values.
    /// </summary>
    internal class StringOnlyEnumConverter : JsonStringEnumConverter
    {
        public StringOnlyEnumConverter()
            : base(namingPolicy: null, allowIntegerValues: false) { }
    }
}
