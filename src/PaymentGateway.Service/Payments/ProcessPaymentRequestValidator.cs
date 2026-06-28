using FluentValidation;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Service.Payments;

public sealed class ProcessPaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    private static readonly string[] AllowedCurrencies = Enum.GetNames<Currency>();

    public ProcessPaymentRequestValidator()
    {
        RuleFor(x => x.CardNumber)
            .NotEmpty().WithMessage("Card number is required.")
            .Matches(@"^\d+$").WithMessage("Card number must only contain numeric characters.")
            .Length(14, 19).WithMessage("Card number must be between 14 and 19 characters long.");

        RuleFor(x => x.ExpiryMonth)
            .InclusiveBetween(1, 12).WithMessage("Expiry month must be between 1 and 12.");

        RuleFor(x => x)
            .Must(BeAValidFutureExpiry)
            .WithName("ExpiryDate")
            .WithMessage("Card expiry date (expiry month and year) must be in the future.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be 3 characters long.")
            .Must(AllowedCurrencies.Contains).WithMessage($"Currency must be one of: {string.Join(", ", AllowedCurrencies)}.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount is required and must be a positive integer.");

        RuleFor(x => x.Cvv)
            .NotEmpty().WithMessage("CVV is required.")
            .Matches(@"^\d+$").WithMessage("CVV must only contain numeric characters.")
            .Length(3, 4).WithMessage("CVV must be 3-4 characters long.");
    }

    private static bool BeAValidFutureExpiry(PaymentRequest request)
    {
        if (request.ExpiryMonth is < 1 or > 12 || request.ExpiryYear is < 1 or > 9999)
        {
            return false;
        }

        var expiryMonthEnd = new DateOnly(request.ExpiryYear, request.ExpiryMonth, 1).AddMonths(1).AddDays(-1);
        return expiryMonthEnd >= DateOnly.FromDateTime(DateTime.UtcNow);
    }
}