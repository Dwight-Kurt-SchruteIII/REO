using System.ComponentModel.DataAnnotations;

namespace RealEstateApi.DTO;

public class CreatePaymentDto
{
    [Range(0, double.MaxValue)] public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(1, int.MaxValue)] public int TenantContractId { get; set; }
}