using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.Models;

public class TenantContract
{
    public int Id { get; set; }

    [Required]
    public string TenantName { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal MonthlyRent { get; set; }

    //[Range(0, double.MaxValue)]
    //doesnt need to be stored, can be calculated as MonthlyRent * 2 or something like that
    //public decimal Deposit { get; set; } 

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int PropertyId { get; set; }

    public Property Property { get; set; } = null!;
}