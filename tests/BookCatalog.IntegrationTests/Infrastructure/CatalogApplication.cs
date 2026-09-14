using BookCatalog.Api;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Serilog.Core;

namespace BookCatalog.IntegrationTests.Infrastructure;

internal sealed class CatalogApplication(
    string connectionString,
    Action<DbContextOptionsBuilder>? configureDatabase = null,
    bool captureLogs = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        if (captureLogs)
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TestLogSink>();
                services.AddSingleton<ILogEventSink>(provider => provider.GetRequiredService<TestLogSink>());
            });
        if (configureDatabase is not null)
            builder.ConfigureServices(services =>
                services.ConfigureDbContext<BookCatalogDbContext>((_, options) => configureDatabase(options)));
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true"
            }));
    }
}