using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTO;

public class PatchTenantContractDto
{
    [MinLength(2)][MaxLength(120)] public string? TenantName { get; set; }

    [Range(0.01, double.MaxValue)] public decimal? MonthlyRent { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Range(1, int.MaxValue)] public int? PropertyId { get; set; }
}