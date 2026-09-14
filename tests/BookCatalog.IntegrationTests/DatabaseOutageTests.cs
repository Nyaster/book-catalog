using System.Net;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.IntegrationTests;

// This class gets its own PostgreSQL container, so pausing it cannot affect other classes.
public sealed class DatabaseOutageTests : ApiTest
{
    private readonly PostgresFixture _postgres;

    public DatabaseOutageTests(PostgresFixture postgres) : base(postgres) => _postgres = postgres;

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.UseNpgsql(postgres => postgres.CommandTimeout(1));

    [Fact]
    public async Task Running_api_returns_503_during_outage_and_recovers_without_restart()
    {
        // Allow real connection timeouts and query cancellation to finish before the client gives up.
        Client.Timeout = TimeSpan.FromSeconds(60);
        var book = await CreateBookAsync();
        using var initialReady = await Client.GetAsync("/health/ready");
        await HealthTests.AssertHealthAsync(initialReady, HttpStatusCode.OK, "Healthy");
        try
        {
            // Pause preserves Docker's random port; stop/start can assign a different port.
            await _postgres.PauseAsync();
            using var live = await Client.GetAsync("/health/live");
            await HealthTests.AssertHealthAsync(live, HttpStatusCode.OK, "Healthy");
            using var ready = await Client.GetAsync("/health/ready");
            await HealthTests.AssertHealthAsync(ready, HttpStatusCode.ServiceUnavailable, "Unhealthy");
            using var response = await Client.GetAsync($"/api/books/{book.Id}");
            await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            await _postgres.ResumeAsync();
        }

        Assert.Equal(book.Id, (await GetBookAsync(book.Id)).Id);
        using var recovered = await Client.GetAsync("/health/ready");
        await HealthTests.AssertHealthAsync(recovered, HttpStatusCode.OK, "Healthy");
    }
}