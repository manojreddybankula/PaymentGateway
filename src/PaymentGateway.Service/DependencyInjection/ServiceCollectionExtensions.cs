using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.Payments;

namespace PaymentGateway.Service.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddServices(this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IValidator<PaymentRequest>, ProcessPaymentRequestValidator>();
    }
}