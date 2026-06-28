using Microsoft.Extensions.Logging;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.DTOs;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Service.Payments;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly IAcquiringBankClient _acquiringBankClient;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentsRepository paymentsRepository,
        IAcquiringBankClient acquiringBankClient,
        ILogger<PaymentService> logger)
    {
        _paymentsRepository = paymentsRepository;
        _acquiringBankClient = acquiringBankClient;
        _logger = logger;
    }
    
    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        var bankRequest = new BankAuthorizationRequest(
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount,
            request.Cvv);
        
        var bankResult = await _acquiringBankClient.AuthorizeAsync(bankRequest, cancellationToken);

        var status = bankResult.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;
        var cardNumberLastFour = request.CardNumber[^4..];
        var currency = Enum.Parse<Currency>(request.Currency);

        var payment = Payment.Create(
            cardNumberLastFour,
            request.ExpiryMonth,
            request.ExpiryYear,
            currency,
            request.Amount,
            status,
            bankResult.AuthorizationCode);

        await _paymentsRepository.AddAsync(payment, cancellationToken);

        _logger.LogDebug(
            "Payment {PaymentId} processed with status {Status} for amount {Amount} {Currency}",
            payment.Id,
            payment.Status,
            payment.Amount,
            payment.Currency);

        return PaymentResult.FromPayment(payment);
    }

    public Task<PaymentResult?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        return _paymentsRepository.GetByIdAsync(id, cancellationToken);
    }
}