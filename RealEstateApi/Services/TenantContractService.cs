using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.DTO;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public class TenantContractService
{
    public enum ChangeTenantContractResult
    {
        Success,
        NotFound,
        ValidationError,
        InvalidProperty,
        OverlapConflict
    }

    private readonly RealEstateDbContext _context;

    public TenantContractService(RealEstateDbContext context)
    {
        _context = context;
    }

    public async Task<List<TenantContract>> GetAllAsync()
    {
        return await _context.TenantContracts
            .Include(tc => tc.Property)
            .ToListAsync();
    }

    public async Task<TenantContract?> GetByIdAsync(int id)
    {
        return await _context.TenantContracts
            .Include(tc => tc.Property)
            .FirstOrDefaultAsync(tc => tc.Id == id);
    }

    public async Task<(ChangeTenantContractResult Result, TenantContract? Contract)> CreateAsync(TenantContract contract)
    {
        if (!IsDateRangeValid(contract.StartDate, contract.EndDate))
            return (ChangeTenantContractResult.ValidationError, null);

        var propertyExists = await _context.Properties.AnyAsync(p => p.Id == contract.PropertyId);
        if (!propertyExists)
            return (ChangeTenantContractResult.InvalidProperty, null);

        var hasOverlap = await HasOverlappingContractAsync(
            contract.PropertyId,
            contract.StartDate,
            contract.EndDate);

        if (hasOverlap)
            return (ChangeTenantContractResult.OverlapConflict, null);

        await _context.TenantContracts.AddAsync(contract);
        await _context.SaveChangesAsync();
        return (ChangeTenantContractResult.Success, contract);
    }

    public async Task<(ChangeTenantContractResult Result, TenantContract? Contract)> UpdateAsync(int id, TenantContract updatedContract)
    {
        var existingContract = await _context.TenantContracts.FindAsync(id);
        if (existingContract == null)
            return (ChangeTenantContractResult.NotFound, null);

        if (!IsDateRangeValid(updatedContract.StartDate, updatedContract.EndDate))
            return (ChangeTenantContractResult.ValidationError, null);

        var propertyExists = await _context.Properties.AnyAsync(p => p.Id == updatedContract.PropertyId);
        if (!propertyExists)
            return (ChangeTenantContractResult.InvalidProperty, null);

        var hasOverlap = await HasOverlappingContractAsync(
            updatedContract.PropertyId,
            updatedContract.StartDate,
            updatedContract.EndDate,
            id);

        if (hasOverlap)
            return (ChangeTenantContractResult.OverlapConflict, null);

        existingContract.TenantName = updatedContract.TenantName;
        existingContract.MonthlyRent = updatedContract.MonthlyRent;
        existingContract.StartDate = updatedContract.StartDate;
        existingContract.EndDate = updatedContract.EndDate;
        existingContract.PropertyId = updatedContract.PropertyId;

        await _context.SaveChangesAsync();
        return (ChangeTenantContractResult.Success, existingContract);
    }

    public async Task<ChangeTenantContractResult> PatchAsync(int id, PatchTenantContractDto patchDto)
    {
        var contract = await _context.TenantContracts.FindAsync(id);
        if (contract == null)
            return ChangeTenantContractResult.NotFound;

        var hasAnyChange = false;

        if (patchDto.TenantName != null)
        {
            contract.TenantName = patchDto.TenantName;
            hasAnyChange = true;
        }

        if (patchDto.MonthlyRent.HasValue)
        {
            contract.MonthlyRent = patchDto.MonthlyRent.Value;
            hasAnyChange = true;
        }

        if (patchDto.StartDate.HasValue)
        {
            contract.StartDate = patchDto.StartDate.Value;
            hasAnyChange = true;
        }

        if (patchDto.EndDate.HasValue)
        {
            contract.EndDate = patchDto.EndDate.Value;
            hasAnyChange = true;
        }

        if (patchDto.PropertyId.HasValue)
        {
            var propertyExists = await _context.Properties.AnyAsync(p => p.Id == patchDto.PropertyId.Value);
            if (!propertyExists)
                return ChangeTenantContractResult.InvalidProperty;

            contract.PropertyId = patchDto.PropertyId.Value;
            hasAnyChange = true;
        }

        if (!hasAnyChange)
            return ChangeTenantContractResult.ValidationError;

        if (!IsDateRangeValid(contract.StartDate, contract.EndDate))
            return ChangeTenantContractResult.ValidationError;

        var hasOverlap = await HasOverlappingContractAsync(
            contract.PropertyId,
            contract.StartDate,
            contract.EndDate,
            id);

        if (hasOverlap)
            return ChangeTenantContractResult.OverlapConflict;

        await _context.SaveChangesAsync();
        return ChangeTenantContractResult.Success;
    }

    public async Task DeleteAsync(int id)
    {
        var contract = await _context.TenantContracts.FindAsync(id);

        if (contract == null)
            return;

        _context.TenantContracts.Remove(contract);
        await _context.SaveChangesAsync();
    }

    private static bool IsDateRangeValid(DateTime startDate, DateTime? endDate)
    {
        return !endDate.HasValue || endDate.Value.Date >= startDate.Date;
    }

    private async Task<bool> HasOverlappingContractAsync(
        int propertyId,
        DateTime startDate,
        DateTime? endDate,
        int? excludedContractId = null)
    {
        var query = _context.TenantContracts
            .AsNoTracking()
            .Where(tc => tc.PropertyId == propertyId);

        if (excludedContractId.HasValue)
        {
            query = query.Where(tc => tc.Id != excludedContractId.Value);
        }

        var existingRanges = await query
            .Select(tc => new { tc.StartDate, tc.EndDate })
            .ToListAsync();

        return existingRanges.Any(range => DateRangesOverlap(
            range.StartDate,
            range.EndDate,
            startDate,
            endDate));
    }

    private static bool DateRangesOverlap(
        DateTime firstStart,
        DateTime? firstEnd,
        DateTime secondStart,
        DateTime? secondEnd)
    {
        var firstStartDate = firstStart.Date;
        var firstEndDate = firstEnd?.Date ?? DateTime.MaxValue.Date;
        var secondStartDate = secondStart.Date;
        var secondEndDate = secondEnd?.Date ?? DateTime.MaxValue.Date;

        return firstStartDate <= secondEndDate && secondStartDate <= firstEndDate;
    }
}