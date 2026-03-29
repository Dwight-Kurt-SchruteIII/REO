using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.Endpoints;

public static class RequestValidation
{
    public static Dictionary<string, string[]> Validate<T>(T request)
    {
        var context = new ValidationContext(request!);
        var validationResults = new List<ValidationResult>();

        Validator.TryValidateObject(request!, context, validationResults, validateAllProperties: true);

        return validationResults
            .SelectMany(result =>
            {
                var members = result.MemberNames.Any()
                    ? result.MemberNames
                    : new[] { string.Empty };

                return members.Select(member => new
                {
                    Member = member,
                    Error = result.ErrorMessage ?? "Validation failed."
                });
            })
            .GroupBy(item => item.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Error).Distinct().ToArray());
    }
}