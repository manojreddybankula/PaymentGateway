namespace PaymentGateway.Service.Exceptions;

// Thrown when the acquiring bank cannot be reached or returns a server error (e.g. HTTP 503).
// Distinct from a Declined payment, which is a normal business outcome returned by the bank.
public sealed class AcquiringBankUnavailableException : Exception
{
    public AcquiringBankUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}