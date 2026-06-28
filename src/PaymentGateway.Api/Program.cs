using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using PaymentGateway.Api.ErrorHandling;
using PaymentGateway.Api.RateLimiting;
using PaymentGateway.Service.DependencyInjection;
using PaymentGateway.Infrastructure.DependencyInjection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration)
      .ReadFrom.Services(services));

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddServices();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddRateLimiter(options =>
{
    var writeLimit  = builder.Configuration.GetSection("RateLimiting:PaymentsWrite");
    var readLimit   = builder.Configuration.GetSection("RateLimiting:PaymentsRead");

    string GetPartitionKey(HttpContext httpContext)
    {
        //  when API Key implemented, this will become the partition key
        // var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        // if (!string.IsNullOrEmpty(userId)) return $"user_{userId}";
        
        var clientIp = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                       ?? httpContext.Connection.RemoteIpAddress?.ToString();

        return $"ip_{clientIp}";
    }

    // POST /api/payments — Partitioned Sliding Window
    options.AddPolicy(RateLimitPolicies.PaymentsWrite, httpContext =>
    {
        string key = GetPartitionKey(httpContext);

        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit          = writeLimit.GetValue<int>("PermitLimit"),
            Window               = TimeSpan.FromSeconds(writeLimit.GetValue<int>("WindowSeconds")),
            SegmentsPerWindow    = 6, // enforce sub-window fairness
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0  // reject immediately; no queuing
        });
    });

    // GET /api/payments/{id} — Partitioned Fixed Window
    options.AddPolicy(RateLimitPolicies.PaymentsRead, httpContext =>
    {
        string key = GetPartitionKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit          = readLimit.GetValue<int>("PermitLimit"),
            Window               = TimeSpan.FromSeconds(readLimit.GetValue<int>("WindowSeconds")),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit           = 0
        });
    });

    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status   = StatusCodes.Status429TooManyRequests,
                Title    = "Too many requests. Please slow down.",
                Instance = ctx.HttpContext.Request.Path
            }, token);
    };
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}