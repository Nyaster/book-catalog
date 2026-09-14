using BookCatalog.Application.Common.Logging;
using System.Net;
using Serilog.Events;
using System.Net.Http.Json;
using System.Text.Json;
using BookCatalog.Api.Contracts.Books;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookCatalog.IntegrationTests;

public sealed class LoggingTests(PostgresFixture postgres) : ApiTest(postgres)
{
    private readonly DatabaseFailureInterceptor _failures = new();
    private TestLogSink Logs => Services.GetRequiredService<TestLogSink>();
    protected override bool CaptureLogs => true;

    protected override void ConfigureDatabase(DbContextOptionsBuilder options) =>
        options.AddInterceptors(_failures);

    [Fact]
    public async Task Create_book_logs_the_book_id_and_request_result()
    {
        var author = await CreateAuthorAsync();

        using var response = await Client.PostAsJsonAsync("/api/books", BookRequest(author.Id));
        var book = await ReadAsync<BookResponse>(response, HttpStatusCode.Created);
        var traceId = GetTraceId(response);
        Assert.False(string.IsNullOrWhiteSpace(traceId));

        var logs = await Logs.ForTraceAsync(traceId);
        var created = Assert.Single(logs, log => log.EventId() == LogEvents.BookCreated.Id);
        var completed = Assert.Single(logs, log => log.EventId() == LogEvents.RequestCompleted.Id);

        Assert.Equal(book.Id.ToString(), created.Value("BookId")?.ToString());
        Assert.Equal(201, completed.Value("StatusCode"));
        Assert.Equal(LogEventLevel.Information, completed.Level);
    }

    [Fact]
    public async Task Missing_book_logs_information_and_returns_the_same_trace_id()
    {
        using var response = await Client.GetAsync($"/api/books/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        var traceId = GetTraceId(response);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(traceId, problem.RootElement.GetProperty("traceId").GetString());

        var log = Assert.Single(await Logs.ForTraceAsync(traceId));
        Assert.Equal(LogEventLevel.Information, log.Level);
        Assert.Equal(404, log.Value("StatusCode"));
    }

    [Fact]
    public async Task Private_request_data_is_not_logged()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users?search=private-search-value");
        request.Content = JsonContent.Create(new { displayName = "Private User Name" });
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer private-access-token");

        using var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await Logs.ForTraceAsync(GetTraceId(response));
        var output = Logs.JsonOutput();
        Assert.DoesNotContain("Private User Name", output);
        Assert.DoesNotContain("private-search-value", output);
        Assert.DoesNotContain("private-access-token", output);
    }

    [Fact]
    public async Task Database_failure_logs_retry_warnings_and_a_final_error()
    {
        // Keep failing reads until the existing retry limit is reached.
        _failures.Arm(sql => sql.StartsWith("SELECT", StringComparison.Ordinal), failures: 10);

        using var response = await Client.GetAsync("/api/books");

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        var logs = await Logs.ForTraceAsync(GetTraceId(response));
        var retries = logs.Where(log => log.EventId() == LogEvents.DatabaseReadRetry.Id).ToArray();
        Assert.Equal(2, retries.Length);
        Assert.All(retries, log => Assert.Equal(LogEventLevel.Warning, log.Level));

        var completed = Assert.Single(logs, log => log.EventId() == LogEvents.RequestCompleted.Id);
        Assert.Equal(LogEventLevel.Error, completed.Level);
        Assert.Equal(503, completed.Value("StatusCode"));
        Assert.DoesNotContain("private database details", Logs.JsonOutput());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-private-traceparent")]
    [InlineData("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01")]
    public async Task Trace_context_matches_response_and_logs(string? parent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/books/{Guid.NewGuid()}");
        if (parent is not null) request.Headers.TryAddWithoutValidation("traceparent", parent);
        request.Headers.TryAddWithoutValidation("baggage", "private=private-baggage");
        using var response = await Client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        var traceId = GetTraceId(response);
        Assert.Equal(32, traceId.Length);
        if (parent?.StartsWith("00-", StringComparison.Ordinal) == true)
            Assert.Equal("0123456789abcdef0123456789abcdef", traceId);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(traceId, problem.RootElement.GetProperty("traceId").GetString());
        var entry = Assert.Single(await Logs.ForTraceAsync(traceId));
        Assert.Equal(traceId, entry.TraceId?.ToHexString());
        Assert.NotEqual("0123456789abcdef", entry.Value("SpanId"));
        Assert.DoesNotContain("private", Logs.JsonOutput());
    }

    private static string GetTraceId(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("X-Trace-Id"));
}