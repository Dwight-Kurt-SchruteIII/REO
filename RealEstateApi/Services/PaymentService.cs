using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Models;

namespace RealEstateApi.Services;

public class PaymentService
{
    public enum ChangePaymentResult
    {
        Success,
        InvalidTenantContract
    }

    private readonly RealEstateDbContext _context;

    public PaymentService(RealEstateDbContext context)
    {
        _context = context;
    }

    public async Task<List<Payment>> GetPaymentsByTenantContractIdAsync(int tenantContractId)
    {
        return await _context.Payments
            .Where(p => p.TenantContractId == tenantContractId)
            .ToListAsync();
    }

    public async Task<(ChangePaymentResult Result, Payment? Payment)> CreateAsync(Payment payment)
    {
        var tenantContractExists = await _context.TenantContracts
            .AnyAsync(tc => tc.Id == payment.TenantContractId);

        if (!tenantContractExists)
        {
            return (ChangePaymentResult.InvalidTenantContract, null);
        }

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return (ChangePaymentResult.Success, payment);
    }
}