using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.RateLimiting;
using PaymentGateway.Domain.Enums;
using PaymentGateway.Service.Contracts;
using PaymentGateway.Service.Payments;

namespace PaymentGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IValidator<PaymentRequest> _validator;

    public PaymentsController(IPaymentService paymentService, IValidator<PaymentRequest> validator)
    {
        _paymentService = paymentService;
        _validator = validator;
    }
    
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PaymentsWrite)]
    public async Task<ActionResult<PaymentResponse>> ProcessPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var paymentRequest = new PaymentRequest(
            request.CardNumber ?? string.Empty,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency ?? string.Empty,
            request.Amount,
            request.Cvv ?? string.Empty);

        var validationResult = await _validator.ValidateAsync(paymentRequest, cancellationToken);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var result = await _paymentService.ProcessPaymentAsync(paymentRequest, cancellationToken);
        var response = PaymentResponse.FromResult(result);
        if (response.Status == PaymentStatus.Declined)
        {
            return StatusCode(StatusCodes.Status502BadGateway, response);
        }
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.PaymentsRead)]
    public async Task<ActionResult<PaymentResponse>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetPaymentAsync(id, cancellationToken);

        return result is null ? NotFound() : Ok(PaymentResponse.FromResult(result));
    }
}