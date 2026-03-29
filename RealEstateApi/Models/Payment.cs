using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.Models;

public class Payment
{
    public int Id { get; set; }

    [Range(0, double.MaxValue)] public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public DateTime? DueDate { get; set; }

    public int TenantContractId { get; set; }
    [Required] public TenantContract TenantContract { get; set; } = null!;

}