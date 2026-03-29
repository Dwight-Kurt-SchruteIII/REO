using RealEstateApi.DTO;
using RealEstateApi.Services;

namespace RealEstateApi.Endpoints;

public static class PaymentEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapPost("/payments", async (CreatePaymentDto paymentDto, PaymentService paymentService) =>
        {
            var validationErrors = RequestValidation.Validate(paymentDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var createResult = await paymentService.CreateAsync(paymentDto.ToEntity());

            if (createResult.Result == PaymentService.ChangePaymentResult.InvalidTenantContract)
            {
                return ApiErrorResponses.Validation(
                    $"Tenant contract with id '{paymentDto.TenantContractId}' was not found.",
                    "payment_invalid_tenant_contract");
            }

            return Results.Created($"/payments/{createResult.Payment!.Id}", createResult.Payment.ToDto());
        })
        .RequireAuthorization("WriteAccess");

        app.MapGet("/tenantcontracts/{tenantContractId}/payments", async (int tenantContractId, PaymentService paymentService) =>
        {
            var payments = await paymentService.GetPaymentsByTenantContractIdAsync(tenantContractId);
            return Results.Ok(payments.Select(p => p.ToDto()));
        });
    }
}