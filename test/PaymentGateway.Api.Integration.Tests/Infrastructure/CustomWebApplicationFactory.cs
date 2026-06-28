using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

using PaymentGateway.Api.Integration.Tests.Fakes;
using PaymentGateway.Service.Contracts;

namespace PaymentGateway.Api.Integration.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string MongoConnectionString = "mongodb://localhost:27018";

    private readonly string _testDatabaseName = $"paymentgateway-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDB:ConnectionString"] = MongoConnectionString,
                ["MongoDB:DatabaseName"] = _testDatabaseName
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton<IAcquiringBankClient, FakeAcquiringBankClient>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            var client = new MongoClient(MongoConnectionString);
            client.DropDatabase(_testDatabaseName);
        }
    }
}