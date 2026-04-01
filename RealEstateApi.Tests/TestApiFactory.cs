using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealEstateApi.Data;

namespace RealEstateApi.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    static TestApiFactory()
    {
        Environment.SetEnvironmentVariable("JWT__SecretKey", "ci-test-only-key-not-used-in-prod-xxxxx");
        Environment.SetEnvironmentVariable("JWT__Issuer", "Api");
        Environment.SetEnvironmentVariable("JWT__Audience", "ApiUsers");
        Environment.SetEnvironmentVariable("JWT__ExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Auth__AdminPassword", "ci-test-admin");
        Environment.SetEnvironmentVariable("Auth__ManagerPassword", "ci-test-manager");
    }

    private readonly string _databaseName = $"RealEstateApiTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<RealEstateDbContext>));
            services.RemoveAll(typeof(RealEstateDbContext));

            services.AddDbContext<RealEstateDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RealEstateDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}