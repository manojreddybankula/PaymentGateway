using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Polly;
using Polly.Extensions.Http;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Infrastructure.AcquiringBank;
using PaymentGateway.Infrastructure.Persistence.Documents;
using PaymentGateway.Infrastructure.Persistence.Repositories;

namespace PaymentGateway.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(opts =>
        {
            opts.ConnectionString = configuration["MongoDB:ConnectionString"] ?? string.Empty;
            opts.DatabaseName = configuration["MongoDB:DatabaseName"] ?? string.Empty;
        });

        services.AddSingleton<IMongoClient>(provider =>
        {
            var opts = provider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            if (string.IsNullOrEmpty(opts.ConnectionString))
                throw new InvalidOperationException("Configuration 'MongoDB:ConnectionString' is not configured.");
            return new MongoClient(opts.ConnectionString);
        });

        services.AddSingleton(provider =>
        {
            var opts = provider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            if (string.IsNullOrEmpty(opts.DatabaseName))
                throw new InvalidOperationException("Configuration 'MongoDB:DatabaseName' is not configured.");
            return provider.GetRequiredService<IMongoClient>()
                .GetDatabase(opts.DatabaseName)
                .GetCollection<PaymentDocument>("payments");
        });

        services.AddScoped<IPaymentsRepository>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<MongoPaymentsRepository>>();
            var collection = provider.GetRequiredService<IMongoCollection<PaymentDocument>>();
            var policy = BuildMongoRetryPolicy(logger, retryCount: 3);
            return new MongoPaymentsRepository(collection, policy);
        });

        var acquiringBankBaseUrl = configuration["AcquiringBank:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration 'AcquiringBank:BaseUrl' is not configured.");

        services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>(client =>
        {
            client.BaseAddress = new Uri(acquiringBankBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddPolicyHandler((provider, _) => BuildRetryPolicy(
            provider.GetRequiredService<ILogger<AcquiringBankClient>>(),
            retryCount: 3));
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(ILogger logger, int retryCount) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    var reason = outcome.Exception?.Message
                        ?? $"HTTP {(int?)outcome.Result?.StatusCode}";
                    logger.LogDebug(
                        "Acquiring bank retry {Attempt}/{RetryCount} in {Delay:F1}s — {Reason}",
                        attempt, retryCount, delay.TotalSeconds, reason);
                });

    private static IAsyncPolicy BuildMongoRetryPolicy(ILogger logger, int retryCount) =>
        Policy
            .Handle<MongoConnectionException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        "MongoDB retry {Attempt}/{RetryCount} in {Delay:F1}s — {Reason}",
                        attempt, retryCount, delay.TotalSeconds, exception.Message);
                });
}