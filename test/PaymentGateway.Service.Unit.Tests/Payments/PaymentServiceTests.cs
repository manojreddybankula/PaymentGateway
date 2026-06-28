using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.DTOs;
using PaymentGateway.Service.Exceptions;
using PaymentGateway.Service.Payments;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Service.Unit.Tests.Payments;

[TestFixture]
public class PaymentServiceTests
{
    private Mock<IPaymentsRepository> _paymentsRepository = null!;
    private Mock<IAcquiringBankClient> _acquiringBankClient = null!;
    private PaymentService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _paymentsRepository = new Mock<IPaymentsRepository>();
        _acquiringBankClient = new Mock<IAcquiringBankClient>();
        _sut = new PaymentService(_paymentsRepository.Object, _acquiringBankClient.Object, NullLogger<PaymentService>.Instance);
    }

    private static PaymentRequest ValidRequest(string cardNumber = "2222405343248871") => new(
        CardNumber: cardNumber,
        ExpiryMonth: 4,
        ExpiryYear: DateTime.UtcNow.Year + 2,
        Currency: "GBP",
        Amount: 1050,
        Cvv: "123");

    [Test]
    public async Task ProcessPaymentAsync_WhenBankAuthorizes_PersistsAndReturnsAuthorizedResult()
    {
        _acquiringBankClient
            .Setup(x => x.AuthorizeAsync(It.IsAny<BankAuthorizationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankAuthorizationResult(true, Guid.NewGuid().ToString()));

        var result = await _sut.ProcessPaymentAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Authorized);
        result.CardNumberLastFour.Should().Be("8871");
        _paymentsRepository.Verify(x => x.AddAsync(It.Is<Payment>(p => p.Status == PaymentStatus.Authorized), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_WhenBankDeclines_PersistsAndReturnsDeclinedResult()
    {
        _acquiringBankClient
            .Setup(x => x.AuthorizeAsync(It.IsAny<BankAuthorizationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BankAuthorizationResult(false, null));

        var result = await _sut.ProcessPaymentAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(PaymentStatus.Declined);
        _paymentsRepository.Verify(x => x.AddAsync(It.Is<Payment>(p => p.Status == PaymentStatus.Declined), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessPaymentAsync_WhenBankIsUnavailable_PropagatesExceptionWithoutPersisting()
    {
        _acquiringBankClient
            .Setup(x => x.AuthorizeAsync(It.IsAny<BankAuthorizationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AcquiringBankUnavailableException("Bank is down."));

        var act = async () => await _sut.ProcessPaymentAsync(ValidRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<AcquiringBankUnavailableException>();
        _paymentsRepository.Verify(x => x.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetPaymentAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var expected = new PaymentResult(id, PaymentStatus.Authorized, "1234", 4, 2030, Currency.GBP, 1050, "AUTH123");
        _paymentsRepository.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetPaymentAsync(id, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Test]
    public async Task GetPaymentAsync_WhenNotFound_ReturnsNull()
    {
        _paymentsRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PaymentResult?)null);

        var result = await _sut.GetPaymentAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}