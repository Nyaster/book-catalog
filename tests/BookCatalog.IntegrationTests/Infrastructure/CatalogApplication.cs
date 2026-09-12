using BookCatalog.Api;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BookCatalog.IntegrationTests.Infrastructure;

internal sealed class CatalogApplication(
    string connectionString,
    Action<DbContextOptionsBuilder>? configureDatabase = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (configureDatabase is not null)
            builder.ConfigureServices(services =>
                services.ConfigureDbContext<BookCatalogDbContext>((_, options) => configureDatabase(options)));
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Logging:LogLevel:Default"] = "Warning"
            }));
    }
}