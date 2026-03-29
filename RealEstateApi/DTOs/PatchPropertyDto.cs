using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTO;

public class PatchPropertyDto
{
    [MinLength(3)][MaxLength(150)] public string? Name { get; set; }

    [MinLength(5)][MaxLength(250)] public string? Address { get; set; }

    [Range(0, double.MaxValue)] public decimal? PurchasePrice { get; set; }

    [Range(0, double.MaxValue)] public decimal? CurrentValue { get; set; }

    public bool? House { get; set; }

    public DateTime? PurchaseDate { get; set; }
}