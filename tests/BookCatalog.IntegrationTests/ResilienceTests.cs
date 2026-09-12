using System.Net;
using System.Net.Http.Json;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Infrastructure.Persistence;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookCatalog.IntegrationTests;

public sealed class ResilienceTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly DatabaseFailureInterceptor _failures = new();

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) => options.AddInterceptors(_failures);

    [Theory]
    [InlineData("books")]
    [InlineData("authors")]
    [InlineData("users")]
    [InlineData("loans")]
    public async Task Read_recovers_after_one_transient_failure(string resource)
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        var uri = resource switch
        {
            "books" => $"/api/books/{book.Id}",
            "authors" => $"/api/authors/{book.AuthorId}",
            "users" => $"/api/users/{user.Id}",
            _ => $"/api/loans/{loan.Id}"
        };
        _failures.Arm(sql => sql.StartsWith("SELECT", StringComparison.Ordinal), failures: 1);

        using var response = await Client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _failures.Attempts);
    }

    [Fact]
    public async Task Page_retries_count_and_items_together()
    {
        await CreateBookAsync();
        // Let COUNT succeed, then fail when fetching the page items.
        _failures.Arm(sql => sql.StartsWith("SELECT", StringComparison.Ordinal), failures: 1, firstFailure: 2);

        using var response = await Client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, _failures.Attempts);
    }

    [Fact]
    public async Task Persistent_read_failure_returns_503_after_three_attempts()
    {
        _failures.Arm(sql => sql.StartsWith("SELECT", StringComparison.Ordinal), failures: 10);

        using var response = await Client.GetAsync("/api/books");

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(3, _failures.Attempts);
        Assert.DoesNotContain("private database details", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Failed_create_is_not_retried()
    {
        _failures.Arm(sql => sql.Contains("INSERT INTO", StringComparison.Ordinal), failures: 1);

        using var response = await Client.PostAsJsonAsync("/api/authors", new { name = "Failed author" });

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, _failures.Attempts);
        _failures.Disarm();
        using var scope = Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>().Authors.CountAsync());
    }

    [Fact]
    public async Task Failed_borrow_is_not_retried_and_rolls_back_availability()
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        _failures.Arm(sql => sql.Contains("INSERT INTO \"Loans\"", StringComparison.Ordinal), failures: 1);

        using var response = await Client.PostAsJsonAsync($"/api/books/{book.Id}/loans", new { userId = user.Id });

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, _failures.Attempts);
        _failures.Disarm();
        Assert.True((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Empty((await BookHistoryAsync(book.Id)).Items);
    }

    [Fact]
    public async Task Failed_return_is_not_retried_and_rolls_back_loan()
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        _failures.Arm(sql => sql.StartsWith("UPDATE \"Books\"", StringComparison.Ordinal), failures: 1);

        using var response = await Client.PostAsJsonAsync($"/api/loans/{loan.Id}/return", new { userId = user.Id });

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, _failures.Attempts);
        _failures.Disarm();
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Null(Assert.Single((await BookHistoryAsync(book.Id)).Items).ReturnedAt);
    }

    [Fact]
    public async Task Read_inside_a_transaction_is_not_retried()
    {
        var book = await CreateBookAsync();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BookCatalogDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IBookRepository>();
        _failures.Arm(sql => sql.StartsWith("SELECT", StringComparison.Ordinal), failures: 1);

        var exception = await Record.ExceptionAsync(() => repository.GetByIdAsync(book.Id));

        Assert.NotNull(exception);
        Assert.True(DatabaseFailures.IsTransient(exception));
        Assert.Equal(1, _failures.Attempts);
    }
}
