using System.Diagnostics;
using System.Net;
using BookCatalog.Api.Health;
using BookCatalog.Infrastructure.Persistence;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace BookCatalog.IntegrationTests;

public sealed class HealthTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly DatabasePauseInterceptor _pause = new();
    protected override bool CaptureLogs => true;
    protected override void ConfigureDatabase(DbContextOptionsBuilder options) => options.AddInterceptors(_pause);

    [Theory]
    [InlineData("live")]
    [InlineData("ready")]
    public async Task Empty_catalog_is_healthy(string probe)
    {
        using var response = await Client.GetAsync($"/health/{probe}");
        await AssertHealthAsync(response, HttpStatusCode.OK, "Healthy");
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task Missing_catalog_table_is_not_ready_and_does_not_expose_diagnostics()
    {
        using var scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>();
        await database.Database.ExecuteSqlRawAsync("ALTER TABLE \"Books\" RENAME TO \"private_missing_table\"");

        using var ready = await Client.GetAsync("/health/ready");
        await AssertHealthAsync(ready, HttpStatusCode.ServiceUnavailable, "Unhealthy");
        using var live = await Client.GetAsync("/health/live");
        await AssertHealthAsync(live, HttpStatusCode.OK, "Healthy");
        var logs = Services.GetRequiredService<TestLogSink>();
        await logs.ForTraceAsync(ready.Headers.GetValues("X-Trace-Id").Single());
        Assert.DoesNotContain("private_missing_table", logs.JsonOutput());
        Assert.DoesNotContain("SELECT", logs.JsonOutput());
    }

    [Fact]
    public async Task Readiness_deadline_cancels_a_blocked_query()
    {
        _pause.Arm(sql => sql.Contains("FROM \"Books\"", StringComparison.Ordinal));
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = Client.GetAsync("/health/ready");
            await _pause.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            using var response = await request.WaitAsync(TimeSpan.FromSeconds(10));
            await AssertHealthAsync(response, HttpStatusCode.ServiceUnavailable, "Unhealthy");
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
        }
        finally
        {
            _pause.Release();
        }
    }

    [Fact]
    public async Task Stopping_is_not_ready_and_does_not_query_database()
    {
        _pause.Arm(_ => true);
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();
        using var scope = Services.CreateScope();
        var check = new ReadinessHealthCheck(scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>(),
            new StoppingLifetime(stopping.Token));
        var result = await check.CheckHealthAsync(new HealthCheckContext()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False(_pause.Entered.IsCompleted);
    }

    internal static async Task AssertHealthAsync(HttpResponseMessage response, HttpStatusCode status, string body)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }

    private sealed class StoppingLifetime(CancellationToken stopping) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => throw new NotSupportedException();
    }
}

public sealed class ProductionHealthTests(PostgresFixture postgres) : ApiTest(postgres)
{
    protected override string EnvironmentName => "Production";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);

    [Theory]
    [InlineData("live")]
    [InlineData("ready")]
    public async Task Http_probe_does_not_redirect_in_production(string probe)
    {
        // Kestrel's underlying HTTP handler can follow redirects despite factory ClientOptions.
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        client.BaseAddress = Client.BaseAddress;
        client.Timeout = TimeSpan.FromSeconds(10);
        using var response = await client.GetAsync($"/health/{probe}");
        await HealthTests.AssertHealthAsync(response, HttpStatusCode.OK, "Healthy");
        using var api = await client.GetAsync("/api/books");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, api.StatusCode);
        Assert.Equal("https", api.Headers.Location?.Scheme);
    }
}