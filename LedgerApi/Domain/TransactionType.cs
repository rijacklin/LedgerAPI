using System.Text.Json.Serialization;

namespace LedgerApi.Domain
{
	// serialize enum values as strings; integer values are rejected.
	[JsonConverter(typeof(StringOnlyEnumConverter))]
	public enum TransactionType { Charge, Refund, Payout }

	public class StringOnlyEnumConverter : JsonStringEnumConverter
	{
		public StringOnlyEnumConverter() : base(namingPolicy: null, allowIntegerValues: false) { }
	}
}
