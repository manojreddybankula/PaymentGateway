namespace PaymentGateway.Api.Models.Requests;

public sealed record PostPaymentRequest(
    string? CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string? Currency,
    int Amount,
    string? Cvv);