using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Exceptions;

namespace PaymentGateway.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string CardNumberLastFour { get; private set; } = string.Empty;
    public int ExpiryMonth { get; private set; }
    public int ExpiryYear { get; private set; }
    public Currency Currency { get; private set; }
    public int Amount { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Payment()
    {
    }

    public static Payment Create(
        string cardNumberLastFour,
        int expiryMonth,
        int expiryYear,
        Currency currency,
        int amount,
        PaymentStatus status,
        string? authorizationCode = null)
    {
        if (cardNumberLastFour is null || cardNumberLastFour.Length != 4 || !cardNumberLastFour.All(char.IsDigit))
        {
            throw new DomainValidationException("Card number last four digits must be exactly 4 numeric characters.");
        }

        if (expiryMonth is < 1 or > 12)
        {
            throw new DomainValidationException("Expiry month must be between 1 and 12.");
        }

        if (amount <= 0)
        {
            throw new DomainValidationException("Amount must be a positive integer representing the minor currency unit.");
        }

        if (status == PaymentStatus.Rejected)
        {
            throw new DomainValidationException("Rejected payments are not persisted; they never reach the Domain layer.");
        }

        return new Payment
        {
            Id = Guid.NewGuid(),
            CardNumberLastFour = cardNumberLastFour,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Currency = currency,
            Amount = amount,
            Status = status,
            AuthorizationCode = authorizationCode,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}