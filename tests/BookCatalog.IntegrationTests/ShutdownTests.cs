using System.Net;
using System.Net.Http.Json;
using BookCatalog.Api.Contracts.Loans;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookCatalog.IntegrationTests;

public sealed class ShutdownTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly DatabasePauseInterceptor _pause = new();
    protected override void ConfigureDatabase(DbContextOptionsBuilder options) => options.AddInterceptors(_pause);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shutdown_drains_borrow_or_rolls_back_when_deadline_expires(bool expires)
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var connectionString = ConnectionString;
        var lifetime = Services.GetRequiredService<IHostApplicationLifetime>();
        var timeout = Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(30), timeout.ShutdownTimeout);
        if (expires) timeout.ShutdownTimeout = TimeSpan.FromMilliseconds(300);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStopped.Register(() => stopped.TrySetResult());
        _pause.Arm(sql => sql.Contains("INSERT INTO \"Loans\"", StringComparison.Ordinal));

        var request = Client.PostAsJsonAsync($"/api/books/{book.Id}/loans", new { userId = user.Id });
        try
        {
            await _pause.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            lifetime.StopApplication();
            Assert.True(lifetime.ApplicationStopping.IsCancellationRequested);
            Assert.False(stopped.Task.IsCompleted);
            if (!expires)
            {
                _pause.Release();
                using var response = await request.WaitAsync(TimeSpan.FromSeconds(10));
                var loan = await ReadAsync<LoanResponse>(response, HttpStatusCode.Created);
                Assert.Equal(book.Id, loan.BookId);
                Assert.Equal(user.Id, loan.UserId);
            }
            else
            {
                await Assert.ThrowsAsync<HttpRequestException>(async () =>
                {
                    using var response = await request.WaitAsync(TimeSpan.FromSeconds(10));
                });
            }
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await AssertPersistedStateAsync(connectionString, book.Id, expires);
        }
        finally { _pause.Release(); }
    }

    private static async Task AssertPersistedStateAsync(string connectionString, Guid bookId, bool rolledBack)
    {
        // A fresh connection and row lock wait for any transaction cleanup still finishing after StopAsync.
        await using var connection = new NpgsqlConnection(connectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await connection.OpenAsync(timeout.Token);
        await using var transaction = await connection.BeginTransactionAsync(timeout.Token);
        await using var book = new NpgsqlCommand(
            "SELECT \"IsAvailable\" FROM \"Books\" WHERE \"Id\" = @id FOR UPDATE", connection, transaction);
        book.Parameters.AddWithValue("id", bookId);
        Assert.Equal(rolledBack, await book.ExecuteScalarAsync(timeout.Token));
        await using var loans = new NpgsqlCommand(
            "SELECT count(*) FROM \"Loans\" WHERE \"BookId\" = @id AND \"ReturnedAt\" IS NULL", connection, transaction);
        loans.Parameters.AddWithValue("id", bookId);
        Assert.Equal(rolledBack ? 0L : 1L, await loans.ExecuteScalarAsync(timeout.Token));
    }
}
