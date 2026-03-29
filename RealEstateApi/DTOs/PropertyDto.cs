namespace RealEstateApi.DTO;

public class PropertyDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public bool House { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal CurrentValue { get; set; }

    public DateTime PurchaseDate { get; set; }
}