using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Service.DTOs;

public sealed record PaymentResult(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    Currency Currency,
    int Amount,
    string? AuthorizationCode = null)
{
    public static PaymentResult FromPayment(Payment payment) => new(
        payment.Id,
        payment.Status,
        payment.CardNumberLastFour,
        payment.ExpiryMonth,
        payment.ExpiryYear,
        payment.Currency,
        payment.Amount,
        payment.AuthorizationCode);
}