using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xenasys.Persistence.Data;
using Xenasys.Persistence.Seeding;

namespace Xenasys.Persistence;

// ==========================================================================================
// WHAT: Startup helper that migrates the database then runs DatabaseSeeder.
// WHY: Ensures schema + baseline data exist before the API serves traffic (M0/M1 gate).
// EXAMPLE: await DatabaseInitializer.InitializeAsync(app.Services) from Program.cs.
// ==========================================================================================
public static class DatabaseInitializer
{
    // ==========================================================================================
    // WHAT: Creates a scope, MigrateAsync, then SeedAsync on ApplicationDbContext.
    // WHY: Scoped DbContext/logger lifetime; migrations must finish before seed natural keys.
    // EXAMPLE: MigrateAsync → SeedAsync → "Database initialization complete."
    // ==========================================================================================
    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseSeeder>>();
        var seeder = new DatabaseSeeder(db, logger);

        logger.LogInformation("Applying EF Core migrations…");
        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Running database seed engine…");
        await seeder.SeedAsync(cancellationToken);
        logger.LogInformation("Database initialization complete.");
    }
}
