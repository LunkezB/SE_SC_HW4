using System.Text.Json.Serialization;

namespace PaymentsService.Models
{
    public sealed record CreateAccountRequest(
        [property: JsonPropertyName("user_id")]
        Guid? UserId = null
    );

    public sealed record CreateAccountResponse(
        [property: JsonPropertyName("account_id")]
        Guid AccountId,
        [property: JsonPropertyName("user_id")]
        Guid UserId,
        [property: JsonPropertyName("balance")]
        decimal Balance
    );

    public sealed record TopUpRequest(
        [property: JsonPropertyName("account_id")]
        Guid? AccountId,
        [property: JsonPropertyName("amount")] decimal Amount
    );

    public sealed record BalanceResponse(
        [property: JsonPropertyName("account_id")]
        Guid AccountId,
        [property: JsonPropertyName("user_id")]
        Guid UserId,
        [property: JsonPropertyName("balance")]
        decimal Balance
    );
}