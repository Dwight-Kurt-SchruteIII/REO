namespace RealEstateApi.Endpoints;

public static class ApiErrorResponses
{
    public static IResult NotFound(string detail, string code = "not_found")
    {
        return Problem(StatusCodes.Status404NotFound, "Resource not found", detail, code);
    }

    public static IResult Validation(string detail, string code = "validation_error")
    {
        return Problem(StatusCodes.Status400BadRequest, "Validation failed", detail, code);
    }

    public static IResult Conflict(string detail, string code = "conflict")
    {
        return Problem(StatusCodes.Status409Conflict, "Conflict", detail, code);
    }

    private static IResult Problem(int statusCode, string title, string detail, string code)
    {
        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code
            });
    }
}
