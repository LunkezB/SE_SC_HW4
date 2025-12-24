using System.Text.Json.Serialization;

namespace OrdersService.Models
{
    public static class EventTypes
    {
        public const string PaymentRequested = "PaymentRequested";
        public const string PaymentResult = "PaymentResult";
    }

    public sealed record PaymentRequestedEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("message_id")]
        Guid MessageId,
        [property: JsonPropertyName("order_id")]
        Guid OrderId,
        [property: JsonPropertyName("user_id")]
        Guid UserId,
        [property: JsonPropertyName("amount")] decimal Amount
    );

    public sealed record PaymentResultEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("message_id")]
        Guid MessageId,
        [property: JsonPropertyName("order_id")]
        Guid OrderId,
        [property: JsonPropertyName("success")]
        bool Success,
        [property: JsonPropertyName("reason")] string? Reason
    );
}