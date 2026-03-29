using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.Models;

public class Property
{
    public int Id { get; set; }

    [Required]
    [MinLength(3)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    public bool House { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PurchasePrice { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CurrentValue { get; set; }

    public DateTime PurchaseDate { get; set; }

    public List<TenantContract> TenantContracts { get; set; } = new();
}