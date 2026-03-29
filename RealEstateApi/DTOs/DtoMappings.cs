using RealEstateApi.Models;

namespace RealEstateApi.DTO;

public static class DtoMappings
{
    public static PropertyDto ToDto(this Property property)
    {
        return new PropertyDto
        {
            Id = property.Id,
            Name = property.Name,
            Address = property.Address,
            House = property.House,
            PurchasePrice = property.PurchasePrice,
            CurrentValue = property.CurrentValue,
            PurchaseDate = property.PurchaseDate
        };
    }

    public static Property ToEntity(this CreatePropertyDto dto)
    {
        return new Property
        {
            Name = dto.Name,
            Address = dto.Address,
            House = dto.House,
            PurchasePrice = dto.PurchasePrice,
            CurrentValue = dto.CurrentValue,
            PurchaseDate = dto.PurchaseDate
        };
    }

    public static TenantContractDto ToDto(this TenantContract contract)
    {
        return new TenantContractDto
        {
            Id = contract.Id,
            TenantName = contract.TenantName,
            MonthlyRent = contract.MonthlyRent,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            PropertyId = contract.PropertyId
        };
    }

    public static TenantContract ToEntity(this CreateTenantContractDto dto)
    {
        return new TenantContract
        {
            TenantName = dto.TenantName,
            MonthlyRent = dto.MonthlyRent,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            PropertyId = dto.PropertyId
        };
    }
    public static PaymentDto ToDto(this Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            DueDate = payment.DueDate,
            TenantContractId = payment.TenantContractId
        };
    }

    public static Payment ToEntity(this CreatePaymentDto dto)
    {
        return new Payment
        {
            Amount = dto.Amount,
            PaymentDate = dto.PaymentDate,
            DueDate = dto.DueDate,
            TenantContractId = dto.TenantContractId
        };
    }
}