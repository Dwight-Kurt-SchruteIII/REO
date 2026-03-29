using RealEstateApi.DTO;
using RealEstateApi.Services;

namespace RealEstateApi.Endpoints;

public static class PropertyEndpoints
{
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/properties", async (PropertyService service) =>
        {
            var properties = await service.GetAllAsync();
            return Results.Ok(properties.Select(p => p.ToDto()));
        });

        app.MapGet("/properties/{id}", async (int id, PropertyService service) =>
        {
            var property = await service.GetByIdAsync(id);

            return property is null
                ? ApiErrorResponses.NotFound($"Property with id '{id}' was not found.", "property_not_found")
                : Results.Ok(property.ToDto());
        });

        app.MapPost("/properties", async (CreatePropertyDto propertyDto, PropertyService service) =>
        {
            var validationErrors = RequestValidation.Validate(propertyDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var created = await service.CreateAsync(propertyDto.ToEntity());
            return Results.Created($"/properties/{created.Id}", created.ToDto());
        })
        .RequireAuthorization("WriteAccess");

        app.MapPut("/properties/{id}", async (int id, CreatePropertyDto propertyDto, PropertyService service) =>
        {
            var validationErrors = RequestValidation.Validate(propertyDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var result = await service.UpdateAsync(id, propertyDto.ToEntity());

            return result switch
            {
                PropertyService.ChangePropertyResult.NotFound =>
                    ApiErrorResponses.NotFound($"Property with id '{id}' was not found.", "property_not_found"),
                _ => Results.NoContent()
            };
        })
        .RequireAuthorization("WriteAccess");

        app.MapDelete("/properties/{id}", async (int id, PropertyService service) =>
        {
            var result = await service.DeleteAsync(id);

            return result switch
            {
                PropertyService.DeletePropertyResult.NotFound =>
                    ApiErrorResponses.NotFound($"Property with id '{id}' was not found.", "property_not_found"),
                PropertyService.DeletePropertyResult.HasContracts =>
                    ApiErrorResponses.Conflict(
                        "Cannot delete property with active tenant contracts.",
                        "property_has_active_contracts"),
                _ => Results.NoContent()
            };
        })
        .RequireAuthorization("WriteAccess");

        app.MapPatch("/properties/{id}", async (int id, PatchPropertyDto patchDto, PropertyService service) =>
        {
            var validationErrors = RequestValidation.Validate(patchDto);
            if (validationErrors.Count != 0)
                return Results.ValidationProblem(validationErrors);

            var result = await service.PatchAsync(id, patchDto);

            return result switch
            {
                PropertyService.PatchPropertyResult.NotFound =>
                    ApiErrorResponses.NotFound($"Property with id '{id}' was not found.", "property_not_found"),
                PropertyService.PatchPropertyResult.ValidationError =>
                    ApiErrorResponses.Validation("Invalid patch payload.", "property_patch_validation_error"),
                _ => Results.NoContent()
            };
        })
        .RequireAuthorization("WriteAccess");
    }
}