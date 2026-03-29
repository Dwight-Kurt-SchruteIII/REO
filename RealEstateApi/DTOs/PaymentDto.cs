namespace RealEstateApi.DTO;

public class PaymentDto
{
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public DateTime? DueDate { get; set; }

    public int TenantContractId { get; set; }
}