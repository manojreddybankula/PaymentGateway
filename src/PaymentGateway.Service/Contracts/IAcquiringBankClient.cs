using PaymentGateway.Service.DTOs;

namespace PaymentGateway.Service.Contracts;

public interface IAcquiringBankClient
{
    Task<BankAuthorizationResult> AuthorizeAsync(BankAuthorizationRequest request, CancellationToken cancellationToken);
}