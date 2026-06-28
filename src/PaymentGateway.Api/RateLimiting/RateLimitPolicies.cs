namespace PaymentGateway.Api.RateLimiting;

internal static class RateLimitPolicies
{
    internal const string PaymentsWrite = "payments-write";
    internal const string PaymentsRead  = "payments-read";
}