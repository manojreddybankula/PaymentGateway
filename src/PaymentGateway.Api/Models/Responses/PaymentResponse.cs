using PaymentGateway.Service.DTOs;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Api.Models.Responses;

public sealed record PaymentResponse(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    Currency Currency,
    int Amount)
{
    public static PaymentResponse FromResult(PaymentResult result) => new(
        result.Id,
        result.Status,
        result.CardNumberLastFour,
        result.ExpiryMonth,
        result.ExpiryYear,
        result.Currency,
        result.Amount);
}