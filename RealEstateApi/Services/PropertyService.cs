using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTO;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public class PropertyService
{
    public enum DeletePropertyResult
    {
        Success,
        NotFound,
        HasContracts
    }

    public enum ChangePropertyResult
    {
        Success,
        NotFound
    }

    public enum PatchPropertyResult
    {
        Success,
        NotFound,
        ValidationError
    }

    private readonly RealEstateDbContext _context;

    public PropertyService(RealEstateDbContext context)
    {
        _context = context;
    }

    public async Task<List<Property>> GetAllAsync()
    {
        return await _context.Properties.ToListAsync();
    }

    public async Task<Property?> GetByIdAsync(int id)
    {
        return await _context.Properties
            .Include(p => p.TenantContracts)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Property> CreateAsync(Property property)
    {
        await _context.Properties.AddAsync(property);
        await _context.SaveChangesAsync();
        return property;
    }
    public async Task<ChangePropertyResult> UpdateAsync(int id, Property updatedProperty)
    {
        var existingProperty = await _context.Properties.FindAsync(id);
        if (existingProperty == null)
            return ChangePropertyResult.NotFound;

        existingProperty.Name = updatedProperty.Name;
        existingProperty.Address = updatedProperty.Address;
        existingProperty.PurchasePrice = updatedProperty.PurchasePrice;
        existingProperty.CurrentValue = updatedProperty.CurrentValue;
        existingProperty.House = updatedProperty.House;
        existingProperty.PurchaseDate = updatedProperty.PurchaseDate;

        await _context.SaveChangesAsync();
        return ChangePropertyResult.Success;
    }


    public async Task<DeletePropertyResult> DeleteAsync(int id)
    {
        var property = await _context.Properties
            .Include(p => p.TenantContracts)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property == null)
            return DeletePropertyResult.NotFound;

        if (property.TenantContracts.Any())
            return DeletePropertyResult.HasContracts;

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync();

        return DeletePropertyResult.Success;
    }
    public async Task<PatchPropertyResult> PatchAsync(int id, PatchPropertyDto patchDto)
    {
        var property = await _context.Properties.FindAsync(id);
        if (property == null)
            return PatchPropertyResult.NotFound;

        var hasAnyChange = false;

        if (patchDto.Name != null)
        {
            property.Name = patchDto.Name;
            hasAnyChange = true;
        }

        if (patchDto.Address != null)
        {
            property.Address = patchDto.Address;
            hasAnyChange = true;
        }

        if (patchDto.PurchasePrice.HasValue)
        {
            property.PurchasePrice = patchDto.PurchasePrice.Value;
            hasAnyChange = true;
        }

        if (patchDto.CurrentValue.HasValue)
        {
            property.CurrentValue = patchDto.CurrentValue.Value;
            hasAnyChange = true;
        }

        if (patchDto.House.HasValue)
        {
            property.House = patchDto.House.Value;
            hasAnyChange = true;
        }

        if (patchDto.PurchaseDate.HasValue)
        {
            property.PurchaseDate = patchDto.PurchaseDate.Value;
            hasAnyChange = true;
        }

        if (!hasAnyChange)
            return PatchPropertyResult.ValidationError;

        await _context.SaveChangesAsync();
        return PatchPropertyResult.Success;
    }
}