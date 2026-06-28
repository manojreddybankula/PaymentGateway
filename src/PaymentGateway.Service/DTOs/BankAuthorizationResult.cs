namespace PaymentGateway.Service.DTOs;

public sealed record BankAuthorizationResult(bool Authorized, string? AuthorizationCode);