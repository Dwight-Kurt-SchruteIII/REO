using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTO;

public class CreateTenantContractDto : IValidatableObject
{
    [Required][MinLength(2)][MaxLength(120)] public string TenantName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)] public decimal MonthlyRent { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Range(1, int.MaxValue)] public int PropertyId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate.HasValue && EndDate.Value.Date < StartDate.Date)
        {
            yield return new ValidationResult(
                "EndDate must be on or after StartDate.",
                new[] { nameof(EndDate) });
        }
    }
}