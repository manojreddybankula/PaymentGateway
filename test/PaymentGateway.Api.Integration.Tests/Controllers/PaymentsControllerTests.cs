using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

using PaymentGateway.Api.Integration.Tests.Infrastructure;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Domain.Enums;

namespace PaymentGateway.Api.Integration.Tests.Controllers;

[TestFixture]
public class PaymentsControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object ValidPostBody(string cardNumber) => new
    {
        cardNumber,
        expiryMonth = 4,
        expiryYear = DateTime.UtcNow.Year + 2,
        currency = "GBP",
        amount = 1050,
        cvv = "123"
    };

    [Test]
    public async Task ProcessPayment_WithCardNumberEndingInOddDigit_ReturnsAuthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPostBody("2222405343248871"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);
        body!.Status.Should().Be(PaymentStatus.Authorized);
        body.CardNumberLastFour.Should().Be("8871");
    }

    [Test]
    public async Task ProcessPayment_WithCardNumberEndingInEvenDigit_ReturnsDeclined()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPostBody("2222405343248872"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);
        body!.Status.Should().Be(PaymentStatus.Declined);
    }

    [Test]
    public async Task ProcessPayment_WhenBankIsUnavailable_Returns503()
    {
        var response = await _client.PostAsJsonAsync("/api/Payments", ValidPostBody("2222405343248870"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task ProcessPayment_WithInvalidRequest_Returns400AndDoesNotCallBank()
    {
        var invalidBody = new { cardNumber = "123", expiryMonth = 13, expiryYear = 2020, currency = "XXX", amount = -5, cvv = "12" };

        var response = await _client.PostAsJsonAsync("/api/Payments", invalidBody);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetPayment_AfterProcessing_ReturnsMatchingPayment()
    {
        var postResponse = await _client.PostAsJsonAsync("/api/Payments", ValidPostBody("2222405343248871"));
        var created = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        var getResponse = await _client.GetAsync($"/api/Payments/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);
        fetched.Should().Be(created);
    }

    [Test]
    public async Task GetPayment_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}