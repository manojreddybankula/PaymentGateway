using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.DTOs;
using PaymentGateway.Service.Exceptions;

namespace PaymentGateway.Infrastructure.AcquiringBank;

public sealed class AcquiringBankClient : IAcquiringBankClient
{
    private const string PaymentsPath = "/payments";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AcquiringBankClient> _logger;

    public AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankAuthorizationResult> AuthorizeAsync(BankAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var payload = new BankPaymentRequestDto(
            request.CardNumber,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear:D4}",
            request.Currency,
            request.Amount,
            request.Cvv);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(PaymentsPath, payload, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach the acquiring bank");
            throw new AcquiringBankUnavailableException("The acquiring bank could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Acquiring bank request timed out");
            throw new AcquiringBankUnavailableException("The acquiring bank request timed out.", ex);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogError("Acquiring bank returned 503 Service Unavailable");
            throw new AcquiringBankUnavailableException("The acquiring bank is currently unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Acquiring bank returned unexpected status code {StatusCode}", (int)response.StatusCode);
            throw new AcquiringBankUnavailableException($"The acquiring bank returned an unexpected status code: {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<BankPaymentResponseDto>(cancellationToken)
            ?? throw new AcquiringBankUnavailableException("The acquiring bank returned an empty response.");

        return new BankAuthorizationResult(body.Authorized, body.AuthorizationCode);
    }
}