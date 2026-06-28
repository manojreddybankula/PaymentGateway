using PaymentGateway.Service.DTOs;
using PaymentGateway.Service.Payments;

namespace PaymentGateway.Service.Contracts;

public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken);

    Task<PaymentResult?> GetPaymentAsync(Guid id, CancellationToken cancellationToken);
}