using System.Text.Json.Serialization;

namespace OrdersService.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderStatus
    {
        NEW,
        FINISHED,
        CANCELED
    }

    public sealed record CreateOrderRequest(
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("description")]
        string? Description,
        [property: JsonPropertyName("user_id")]
        Guid? UserId = null
    );

    public sealed record OrderDto(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("user_id")]
        Guid UserId,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("description")]
        string? Description,
        [property: JsonPropertyName("status")] OrderStatus Status,
        [property: JsonPropertyName("created_at")]
        DateTimeOffset CreatedAt,
        [property: JsonPropertyName("updated_at")]
        DateTimeOffset UpdatedAt
    );

    public sealed record OrderStatusResponse(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("status")] OrderStatus Status
    );
}