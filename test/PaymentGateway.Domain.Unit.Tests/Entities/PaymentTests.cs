using FluentAssertions;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Domain.Exceptions;

namespace PaymentGateway.Domain.Unit.Tests.Entities;

[TestFixture]
public class PaymentTests
{
    [Test]
    public void Create_WithValidData_ReturnsPaymentWithExpectedValues()
    {
        var payment = Payment.Create("1234", 4, 2030, Currency.GBP, 1050, PaymentStatus.Authorized);

        payment.Id.Should().NotBe(Guid.Empty);
        payment.CardNumberLastFour.Should().Be("1234");
        payment.ExpiryMonth.Should().Be(4);
        payment.ExpiryYear.Should().Be(2030);
        payment.Currency.Should().Be(Currency.GBP);
        payment.Amount.Should().Be(1050);
        payment.Status.Should().Be(PaymentStatus.Authorized);
    }

    [TestCase("123")]
    [TestCase("12345")]
    [TestCase("12ab")]
    [TestCase("")]
    public void Create_WithInvalidLastFourDigits_ThrowsDomainValidationException(string lastFour)
    {
        var act = () => Payment.Create(lastFour, 4, 2030, Currency.GBP, 1050, PaymentStatus.Authorized);

        act.Should().Throw<DomainValidationException>();
    }

    [TestCase(0)]
    [TestCase(13)]
    public void Create_WithInvalidExpiryMonth_ThrowsDomainValidationException(int expiryMonth)
    {
        var act = () => Payment.Create("1234", expiryMonth, 2030, Currency.GBP, 1050, PaymentStatus.Authorized);

        act.Should().Throw<DomainValidationException>();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Create_WithNonPositiveAmount_ThrowsDomainValidationException(int amount)
    {
        var act = () => Payment.Create("1234", 4, 2030, Currency.GBP, amount, PaymentStatus.Authorized);

        act.Should().Throw<DomainValidationException>();
    }

    [Test]
    public void Create_WithRejectedStatus_ThrowsDomainValidationException()
    {
        var act = () => Payment.Create("1234", 4, 2030, Currency.GBP, 1050, PaymentStatus.Rejected);

        act.Should().Throw<DomainValidationException>();
    }
}