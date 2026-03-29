namespace RealEstateApi.DTO;

public class TenantContractDto
{
    public int Id { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public decimal MonthlyRent { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int PropertyId { get; set; }
}