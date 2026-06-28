using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Infrastructure.Persistence.Documents;

internal sealed class PaymentDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; }

    [BsonRepresentation(BsonType.String)]
    public PaymentStatus Status { get; init; }

    public string CardNumberLastFour { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }

    [BsonRepresentation(BsonType.String)]
    public Currency Currency { get; init; }

    public int Amount { get; init; }
    public string? AuthorizationCode { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}