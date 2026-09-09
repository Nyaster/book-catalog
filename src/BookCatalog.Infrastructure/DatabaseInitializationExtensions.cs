using BookCatalog.Infrastructure.Configuration;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookCatalog.Infrastructure;

public static class DatabaseInitializationExtensions
{
    public static async Task ApplyMigrationsAsync(this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (!options.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");
        
        logger.LogInformation("Applying database migrations...");
        
        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation(
            "Database migrations applied successfully."
        );
    }
}