using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

using PaymentGateway.Api.Integration.Tests.Fakes;
using PaymentGateway.Infrastructure.DependencyInjection;
using PaymentGateway.Service.Contracts;

namespace PaymentGateway.Api.Integration.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string MongoConnectionString = "mongodb://localhost:27018";

    private readonly string _testDatabaseName = $"paymentgateway-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBankClient>();
            services.AddSingleton<IAcquiringBankClient, FakeAcquiringBankClient>();

            // PostConfigure always runs after every Configure call, guaranteeing
            // the test connection string wins regardless of configuration timing.
            services.PostConfigure<MongoDbOptions>(opts =>
            {
                opts.ConnectionString = MongoConnectionString;
                opts.DatabaseName = _testDatabaseName;
            });
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
