using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Xenasys.Core.Abstractions;
using Xenasys.Persistence.Data;

namespace Xenasys.Persistence;

/// <summary>
/// Enables `dotnet ef migrations` without a running HTTP request
/// (no JWT tenant context at design time).
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Xenasys.Presentation"));

        if (!Directory.Exists(basePath))
        {
            basePath = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "src", "Xenasys.Presentation"));
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost\\SQLEXPRESS;Database=XenasysMarketplace;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;Pooling=True;Max Pool Size=100;Connection Timeout=30;MultipleActiveResultSets=True";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql =>
            {
                sql.MigrationsAssembly("Xenasys.Persistence");
                sql.CommandTimeout(30);
            });

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new DesignTimeTenantProvider(),
            new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeTenantProvider : ITenantProvider
    {
        public int? TenantId => null;

        public bool HasTenant => false;

        public bool IsSuperAdmin => false;

        public void SetTenant(int tenantId, bool isSuperAdmin = false)
        {
        }
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public int? UserId => null;

        public string? Role => null;

        public string? UserName => null;

        public bool IsAuthenticated => false;
    }
}
