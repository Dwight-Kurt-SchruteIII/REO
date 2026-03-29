using Microsoft.EntityFrameworkCore;
using RealEstateApi.Models;

namespace RealEstateApi.Data;

public class RealEstateDbContext : DbContext
{
    public RealEstateDbContext(DbContextOptions<RealEstateDbContext> options) : base(options)
    {
    }

    public DbSet<Property> Properties { get; set; }
    public DbSet<TenantContract> TenantContracts { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>()
            .HasMany(p => p.TenantContracts)
            .WithOne(tc => tc.Property)
            .HasForeignKey(tc => tc.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Property>()
            .Property(p => p.PurchasePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Property>()
            .Property(p => p.CurrentValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<TenantContract>()
            .Property(tc => tc.MonthlyRent)
            .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);
    }
}