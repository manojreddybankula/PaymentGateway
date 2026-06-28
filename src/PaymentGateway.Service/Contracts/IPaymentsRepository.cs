using PaymentGateway.Service.DTOs;
using PaymentGateway.Domain.Entities;

namespace PaymentGateway.Service.Contracts;

public interface IPaymentsRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    Task<PaymentResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}