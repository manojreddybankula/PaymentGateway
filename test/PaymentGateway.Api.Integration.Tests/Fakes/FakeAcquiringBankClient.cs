using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.DTOs;
using PaymentGateway.Service.Exceptions;

namespace PaymentGateway.Api.Integration.Tests.Fakes;

// Mirrors the documented bank simulator contract without requiring Docker/Mountebank in tests:
// odd last digit -> authorized, even -> declined, zero -> bank unavailable.
public sealed class FakeAcquiringBankClient : IAcquiringBankClient
{
    public Task<BankAuthorizationResult> AuthorizeAsync(BankAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var lastDigit = request.CardNumber[^1] - '0';

        if (lastDigit == 0)
        {
            throw new AcquiringBankUnavailableException("Simulated acquiring bank unavailability for test card ending in 0.");
        }

        var authorized = lastDigit % 2 != 0;
        return Task.FromResult(new BankAuthorizationResult(authorized, authorized ? Guid.NewGuid().ToString() : null));
    }
}