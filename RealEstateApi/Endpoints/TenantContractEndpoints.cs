using RealEstateApi.DTO;
using RealEstateApi.Services;

namespace RealEstateApi.Endpoints;

public static class TenantContractEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapPost("/tenantcontracts", async (CreateTenantContractDto contractDto, TenantContractService service) =>
        {
            var validationErrors = RequestValidation.Validate(contractDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var createResult = await service.CreateAsync(contractDto.ToEntity());

            return createResult.Result switch
            {
                TenantContractService.ChangeTenantContractResult.Success =>
                    Results.Created(
                        $"/tenantcontracts/{createResult.Contract!.Id}",
                        createResult.Contract.ToDto()),
                TenantContractService.ChangeTenantContractResult.InvalidProperty =>
                    ApiErrorResponses.Validation(
                        $"Property with id '{contractDto.PropertyId}' was not found.",
                        "tenant_contract_invalid_property"),
                TenantContractService.ChangeTenantContractResult.OverlapConflict =>
                    ApiErrorResponses.Conflict(
                        "Tenant contract period overlaps with an existing contract for this property.",
                        "tenant_contract_overlap"),
                _ => ApiErrorResponses.Validation(
                    "Invalid tenant contract payload.",
                    "tenant_contract_validation_error")
            };
        })
        .RequireAuthorization("WriteAccess");

        app.MapPut("/tenantcontracts/{id}", async (int id, CreateTenantContractDto contractDto, TenantContractService service) =>
        {
            var validationErrors = RequestValidation.Validate(contractDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var updateResult = await service.UpdateAsync(id, contractDto.ToEntity());

            return updateResult.Result switch
            {
                TenantContractService.ChangeTenantContractResult.Success =>
                    Results.Ok(updateResult.Contract!.ToDto()),
                TenantContractService.ChangeTenantContractResult.NotFound =>
                    ApiErrorResponses.NotFound($"Tenant contract with id '{id}' was not found.", "tenant_contract_not_found"),
                TenantContractService.ChangeTenantContractResult.InvalidProperty =>
                    ApiErrorResponses.Validation(
                        $"Property with id '{contractDto.PropertyId}' was not found.",
                        "tenant_contract_invalid_property"),
                TenantContractService.ChangeTenantContractResult.OverlapConflict =>
                    ApiErrorResponses.Conflict(
                        "Tenant contract period overlaps with an existing contract for this property.",
                        "tenant_contract_overlap"),
                _ => ApiErrorResponses.Validation(
                    "Invalid tenant contract payload.",
                    "tenant_contract_validation_error")
            };
        })
        .RequireAuthorization("WriteAccess");

        app.MapPatch("/tenantcontracts/{id}", async (int id, PatchTenantContractDto patchDto, TenantContractService service) =>
        {
            var validationErrors = RequestValidation.Validate(patchDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var result = await service.PatchAsync(id, patchDto);

            return result switch
            {
                TenantContractService.ChangeTenantContractResult.NotFound =>
                    ApiErrorResponses.NotFound($"Tenant contract with id '{id}' was not found.", "tenant_contract_not_found"),
                TenantContractService.ChangeTenantContractResult.InvalidProperty =>
                    ApiErrorResponses.Validation("Property was not found.", "tenant_contract_invalid_property"),
                TenantContractService.ChangeTenantContractResult.ValidationError =>
                    ApiErrorResponses.Validation("Invalid patch payload.", "tenant_contract_patch_validation_error"),
                TenantContractService.ChangeTenantContractResult.OverlapConflict =>
                    ApiErrorResponses.Conflict(
                        "Tenant contract period overlaps with an existing contract for this property.",
                        "tenant_contract_overlap"),
                _ => Results.NoContent()
            };
        })
        .RequireAuthorization("WriteAccess");

        app.MapGet("/tenantcontracts", async (TenantContractService service) =>
        {
            var contracts = await service.GetAllAsync();
            return Results.Ok(contracts.Select(tc => tc.ToDto()));
        });

        app.MapGet("/tenantcontracts/{id}", async (int id, TenantContractService service) =>
        {
            var contract = await service.GetByIdAsync(id);

            return contract is null
                ? ApiErrorResponses.NotFound($"Tenant contract with id '{id}' was not found.", "tenant_contract_not_found")
                : Results.Ok(contract.ToDto());
        });

        app.MapDelete("/tenantcontracts/{id}", async (int id, TenantContractService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        })
        .RequireAuthorization("WriteAccess");
    }
}