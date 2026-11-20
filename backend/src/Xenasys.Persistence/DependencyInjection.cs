using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xenasys.Core.Abstractions.Auth;
using Xenasys.Core.Abstractions.Coding;
using Xenasys.Core.Abstractions.Validation;
using Xenasys.Persistence.Coding;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Repositories;
using Xenasys.Persistence.Security;
using Xenasys.Persistence.Validation;

namespace Xenasys.Persistence;

// ==========================================================================================
// WHAT: DI registration for EF Core DbContext, auth repositories, and PermissionGate.
// WHY: Keeps Persistence wiring in one place for Program.cs / host composition.
// EXAMPLE: services.AddPersistence(configuration) → SQL Server + AuthRepository scoped.
// ==========================================================================================
public static class DependencyInjection
{
    // ==========================================================================================
    // WHAT: Registers ApplicationDbContext (SQL Server) and auth-related Persistence services.
    // WHY: Central connection string, migrations assembly, retry, and repository lifetimes.
    // EXAMPLE: UseSqlServer(DefaultConnection) + AddAuthRepositories().
    // ==========================================================================================
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing from configuration.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql =>
                {
                    sql.MigrationsAssembly("Xenasys.Persistence");
                    sql.CommandTimeout(120);
                    sql.EnableRetryOnFailure(3);
                }));

        return services.AddAuthRepositories();
    }

    public static IServiceCollection AddAuthRepositories(this IServiceCollection services)
    {
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IPermissionGate, PermissionGate>();
        services.AddScoped<IActiveForeignKeyValidator, ActiveForeignKeyValidator>();
        services.AddScoped<ITenantCodeAllocator, TenantCodeAllocator>();
        services.AddScoped<IRoleCodeAllocator, RoleCodeAllocator>();
        services.AddScoped<ISubsidiaryAccountNoAllocator, SubsidiaryAccountNoAllocator>();
        services.AddScoped<IProductCodeAllocator, ProductCodeAllocator>();
        services.AddScoped<ICatalogCodeAllocator, CatalogCodeAllocator>();
        services.AddScoped<ISalesOrderCodeAllocator, SalesOrderCodeAllocator>();
        return services;
    }
}
