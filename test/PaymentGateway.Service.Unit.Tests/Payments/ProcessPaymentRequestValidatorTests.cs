using FluentAssertions;
using FluentValidation.TestHelper;
using PaymentGateway.Service.Payments;

namespace PaymentGateway.Service.Unit.Tests.Payments;

[TestFixture]
public class ProcessPaymentRequestValidatorTests
{
    private readonly ProcessPaymentRequestValidator _validator = new();

    private static PaymentRequest ValidRequest() => new(
        CardNumber: "2222405343248877",
        ExpiryMonth: 4,
        ExpiryYear: DateTime.UtcNow.Year + 2,
        Currency: "GBP",
        Amount: 1050,
        Cvv: "123");

    [Test]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [TestCase("1234567890123")]    // 13 chars - too short
    [TestCase("12345678901234567890")] // 21 chars - too long
    [TestCase("1234abcd56789012")] // non-numeric
    public void Validate_WithInvalidCardNumber_HasErrorForCardNumber(string cardNumber)
    {
        var request = ValidRequest() with { CardNumber = cardNumber };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [TestCase(0)]
    [TestCase(13)]
    public void Validate_WithInvalidExpiryMonth_HasErrorForExpiryMonth(int expiryMonth)
    {
        var request = ValidRequest() with { ExpiryMonth = expiryMonth };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ExpiryMonth);
    }

    [Test]
    public void Validate_WithExpiryDateInThePast_HasErrorForExpiryDate()
    {
        var request = ValidRequest() with { ExpiryMonth = 1, ExpiryYear = DateTime.UtcNow.Year - 1 };

        var result = _validator.TestValidate(request);

        result.Errors.Should().Contain(e => e.PropertyName == "ExpiryDate");
    }

    [TestCase("")]
    [TestCase("XX")]
    [TestCase("XXX")]
    public void Validate_WithUnsupportedCurrency_HasErrorForCurrency(string currency)
    {
        var request = ValidRequest() with { Currency = currency };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [TestCase(0)]
    [TestCase(-100)]
    public void Validate_WithNonPositiveAmount_HasErrorForAmount(int amount)
    {
        var request = ValidRequest() with { Amount = amount };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [TestCase("12")]
    [TestCase("12345")]
    [TestCase("abc")]
    public void Validate_WithInvalidCvv_HasErrorForCvv(string cvv)
    {
        var request = ValidRequest() with { Cvv = cvv };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }
}