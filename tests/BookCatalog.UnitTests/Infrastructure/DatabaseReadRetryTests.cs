using System.Transactions;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BookCatalog.UnitTests.Infrastructure;

public sealed class DatabaseReadRetryTests
{
    private static BookCatalogDbContext CreateContext() => new(
        new DbContextOptionsBuilder<BookCatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused")
            .Options);

    private static NpgsqlException TemporaryFailure() => new("Test failure", new IOException());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Returns_success_after_bounded_transient_failures(int failures)
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        var attempts = 0;
        var result = await retry.ExecuteAsync("Test.Read", _ =>
        {
            if (++attempts <= failures) throw TemporaryFailure();
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(failures + 1, attempts);
    }

    [Fact]
    public async Task Rethrows_original_wrapped_failure_after_three_attempts()
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        var failure = new InvalidOperationException("EF wrapper", new DbUpdateException("EF wrapper", TemporaryFailure()));
        var attempts = 0;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => retry.ExecuteAsync<int>("Test.Read", _ =>
        {
            attempts++;
            throw failure;
        }));

        Assert.Same(failure, actual);
        Assert.Equal(3, attempts);
    }

    public static TheoryData<Exception> PermanentFailures => new()
    {
        new InvalidOperationException("Application bug"),
        new TimeoutException("Not identified as a database error"),
        new OperationCanceledException(),
        new NpgsqlException("Non-transient provider failure"),
        new PostgresException("Unique violation", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation),
        new PostgresException("Authentication failure", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword)
    };

    [Theory]
    [MemberData(nameof(PermanentFailures))]
    public async Task Does_not_retry_permanent_errors_or_cancellation(Exception failure)
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        var attempts = 0;

        var actual = await Record.ExceptionAsync(() => retry.ExecuteAsync<int>("Test.Read", _ =>
        {
            attempts++;
            throw failure;
        }));

        Assert.Same(failure, actual);
        Assert.Equal(1, attempts);
        Assert.False(DatabaseFailures.IsTransient(failure));
    }

    [Fact]
    public async Task Does_not_start_a_cancelled_read()
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry.ExecuteAsync("Test.Read", _ =>
        {
            attempts++;
            return Task.FromResult(42);
        }, cancellation.Token));
        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task Cancellation_during_retry_delay_prevents_another_attempt()
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;

        var pendingRead = retry.ExecuteAsync<int>("Test.Read", token =>
        {
            Assert.Equal(cancellation.Token, token);
            attempts++;
            throw TemporaryFailure();
        }, cancellation.Token);
        // ExecuteAsync has reached its first asynchronous retry delay.
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Does_not_retry_inside_an_ambient_transaction()
    {
        await using var context = CreateContext();
        var retry = new DatabaseReadRetry(context, NullLogger<DatabaseReadRetry>.Instance);
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        var attempts = 0;

        await Assert.ThrowsAsync<NpgsqlException>(() => retry.ExecuteAsync<int>("Test.Read", _ =>
        {
            attempts++;
            throw TemporaryFailure();
        }));
        Assert.Equal(1, attempts);
    }
}
