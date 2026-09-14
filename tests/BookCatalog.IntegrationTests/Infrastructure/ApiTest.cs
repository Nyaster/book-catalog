using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookCatalog.Api.Contracts.Authors;
using BookCatalog.Api.Contracts.Books;
using BookCatalog.Api.Contracts.Loans;
using BookCatalog.Api.Contracts.Users;
using BookCatalog.Application.Books.Contracts;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookCatalog.IntegrationTests.Infrastructure;

public abstract class ApiTest(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly string _database = $"test_{Guid.NewGuid():N}";
    private CatalogApplication? _application;
    private string? _connectionString;
    private bool _databaseCreationAttempted;
    private HttpClient? _client;
    protected HttpClient Client => _client ?? throw new InvalidOperationException("The test API has not started.");
    protected IServiceProvider Services => _application!.Services;
    protected virtual bool CaptureLogs => false;
    protected virtual string EnvironmentName => "Development";
    protected string ConnectionString => _connectionString!;

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected virtual void ConfigureDatabase(DbContextOptionsBuilder options)
    {
    }

    public async Task InitializeAsync()
    {
        try
        {
            _connectionString = postgres.ConnectionStringFor(_database);
            _databaseCreationAttempted = true;
            await postgres.CreateDatabaseAsync(_database);
            _application = new CatalogApplication(_connectionString, ConfigureDatabase, CaptureLogs,
                EnvironmentName, ConfigureServices);
            _application.UseKestrel(0);
            _application.ClientOptions.AllowAutoRedirect = false;
            _client = _application.CreateClient();
            _client.Timeout = TimeSpan.FromSeconds(15);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
            _client = null;
            var application = _application;
            _application = null;
            if (application is not null)
                await application.DisposeAsync();
        }
        finally
        {
            if (_connectionString is not null)
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                NpgsqlConnection.ClearPool(connection);
            }

            if (_databaseCreationAttempted)
            {
                await postgres.DropDatabaseAsync(_database);
                _databaseCreationAttempted = false;
            }
        }
    }

    protected async Task<T> GetAsync<T>(string uri)
    {
        using var response = await Client.GetAsync(uri);
        return await ReadAsync<T>(response, HttpStatusCode.OK);
    }

    protected async Task<T> CreateAsync<T>(string uri, object request)
    {
        using var response = await Client.PostAsJsonAsync(uri, request);
        var result = await ReadAsync<T>(response, HttpStatusCode.Created);
        Assert.NotNull(response.Headers.Location);
        // A 201 must point to the resource that was actually persisted.
        var retrieved = await GetAsync<T>(response.Headers.Location.ToString());
        if (result is LoanResponse expectedLoan && retrieved is LoanResponse actualLoan)
            AssertSameLoan(expectedLoan, actualLoan);
        else
            Assert.Equal(result, retrieved);
        return retrieved;
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected}, received {(int)response.StatusCode}. Body: {body}");
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web)
               ?? throw new InvalidOperationException("The API returned a null JSON response.");
    }

    protected static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
    }

    protected Task<AuthorResponse> CreateAuthorAsync() =>
        CreateAsync<AuthorResponse>("/api/authors", new { name = "Test Author" });

    protected Task<UserResponse> CreateUserAsync(string name = "Test Borrower") =>
        CreateAsync<UserResponse>("/api/users", new { displayName = name });

    protected async Task<BookResponse> CreateBookAsync(string isbn = "9780140328721")
    {
        var author = await CreateAuthorAsync();
        return await CreateAsync<BookResponse>("/api/books", BookRequest(author.Id, isbn));
    }

    protected static object BookRequest(Guid authorId, string isbn = "9780140328721", string? title = "Matilda") =>
        new { title, authorId, isbn, publicationYear = 1988, description = "Integration test book" };

    protected Task<LoanResponse> BorrowAsync(Guid bookId, Guid userId) =>
        CreateAsync<LoanResponse>($"/api/books/{bookId}/loans", new { userId });

    protected async Task<LoanResponse> ReturnAsync(Guid loanId, Guid userId)
    {
        using var response = await Client.PostAsJsonAsync($"/api/loans/{loanId}/return", new { userId });
        var returned = await ReadAsync<LoanResponse>(response, HttpStatusCode.OK);
        var persisted = await GetAsync<LoanResponse>($"/api/loans/{loanId}");
        AssertSameLoan(returned, persisted);
        return persisted;
    }

    protected static void AssertSameLoan(LoanResponse expected, LoanResponse actual)
    {
        // PostgreSQL stores microseconds; a .NET timestamp may have nine extra 100-nanosecond ticks.
        Assert.InRange((expected.BorrowedAt - actual.BorrowedAt).Duration(), TimeSpan.Zero, TimeSpan.FromTicks(9));
        Assert.Equal(expected.ReturnedAt.HasValue, actual.ReturnedAt.HasValue);
        if (expected.ReturnedAt is { } expectedReturn && actual.ReturnedAt is { } actualReturn)
            Assert.InRange((expectedReturn - actualReturn).Duration(), TimeSpan.Zero, TimeSpan.FromTicks(9));

        Assert.Equal(expected with { BorrowedAt = actual.BorrowedAt, ReturnedAt = actual.ReturnedAt }, actual);
    }

    protected Task<BookResponse> GetBookAsync(Guid bookId) => GetAsync<BookResponse>($"/api/books/{bookId}");

    protected Task<PagedResult<LoanResponse>> BookHistoryAsync(Guid bookId) =>
        GetAsync<PagedResult<LoanResponse>>($"/api/books/{bookId}/loans");
}