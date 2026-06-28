using System.Text.Json.Serialization;

namespace PaymentGateway.Infrastructure.AcquiringBank;

// Wire-format DTOs matching the bank simulator's JSON contract exactly (snake_case).
internal sealed record BankPaymentRequestDto(
    [property: JsonPropertyName("card_number")] string CardNumber,
    [property: JsonPropertyName("expiry_date")] string ExpiryDate,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("cvv")] string Cvv);

internal sealed record BankPaymentResponseDto(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);