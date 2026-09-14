using BookCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BookCatalog.Api.Health;

public sealed class ReadinessHealthCheck(
    BookCatalogDbContext database,
    IHostApplicationLifetime lifetime) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (lifetime.ApplicationStopping.IsCancellationRequested)
            return HealthCheckResult.Unhealthy();

        try
        {
            // An empty catalog is healthy. Read a real table without loading catalog data.
            await database.Books.AsNoTracking().Select(book => book.Id).Take(1)
                .ToArrayAsync(cancellationToken);
            return lifetime.ApplicationStopping.IsCancellationRequested
                ? HealthCheckResult.Unhealthy()
                : HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            // Health responses and logs must not expose provider exception details.
            return HealthCheckResult.Unhealthy();
        }
    }
}
