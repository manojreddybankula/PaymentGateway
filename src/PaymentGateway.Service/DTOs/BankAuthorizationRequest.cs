namespace PaymentGateway.Service.DTOs;

public sealed record BankAuthorizationRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);