using MongoDB.Driver;
using Polly;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.DTOs;
using PaymentGateway.Domain.Entities;
using PaymentGateway.Infrastructure.Persistence.Documents;

namespace PaymentGateway.Infrastructure.Persistence.Repositories;

internal sealed class MongoPaymentsRepository : IPaymentsRepository
{
    private readonly IMongoCollection<PaymentDocument> _collection;
    private readonly IAsyncPolicy _retryPolicy;

    public MongoPaymentsRepository(IMongoCollection<PaymentDocument> collection, IAsyncPolicy retryPolicy)
    {
        _collection = collection;
        _retryPolicy = retryPolicy;
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        var document = new PaymentDocument
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount,
            AuthorizationCode = payment.AuthorizationCode,
            CreatedAtUtc = payment.CreatedAtUtc
        };

        await _retryPolicy.ExecuteAsync(
            ct => _collection.InsertOneAsync(document, cancellationToken: ct),
            cancellationToken);
    }

    public async Task<PaymentResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _retryPolicy.ExecuteAsync(
            ct => _collection.Find(p => p.Id == id).FirstOrDefaultAsync(ct),
            cancellationToken);

        if (document is null)
            return null;

        return new PaymentResult(
            document.Id,
            document.Status,
            document.CardNumberLastFour,
            document.ExpiryMonth,
            document.ExpiryYear,
            document.Currency,
            document.Amount,
            document.AuthorizationCode);
    }
}