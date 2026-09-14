using BookCatalog.Api.ErrorHandling;
using BookCatalog.Api.Logging;
using BookCatalog.Application.Common.Logging;
using BookCatalog.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.Text.Json;

namespace BookCatalog.IntegrationTests;

public sealed class RequestPipelineLoggingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Server_failure_has_one_safe_terminal_event(bool handled)
    {
        await using var host = new PipelineHost(_ => throw new InvalidOperationException("private exception message",
            new Exception("private inner message")), handleExceptions: handled);
        var context = host.Context();
        if (handled)
            await host.Pipeline(context);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => host.Pipeline(context));

        var entry = Assert.Single(host.Logs.Entries);
        Assert.Equal(LogEvents.RequestCompleted.Id, entry.EventId());
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Equal(500, entry.Value("StatusCode"));
        Assert.Equal("Failed", entry.Value("Outcome"));
        Assert.Equal(typeof(InvalidOperationException).FullName, entry.Value("ExceptionType"));
        Assert.NotEmpty(Assert.IsType<string>(entry.Value("StackTrace")));
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("private", host.Logs.JsonOutput());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cancellation_is_information_and_preserves_only_started_status(bool started, bool throws)
    {
        using var cancellation = new CancellationTokenSource();
        await using var host = new PipelineHost(context =>
        {
            context.Response.StatusCode = 202;
            cancellation.Cancel();
            return throws ? Task.FromCanceled(cancellation.Token) : Task.CompletedTask;
        });
        var context = host.Context(started);
        context.RequestAborted = cancellation.Token;
        if (throws)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Pipeline(context));
        else
            await host.Pipeline(context);

        var entry = Assert.Single(host.Logs.Entries);
        Assert.Equal(LogEvents.RequestAborted.Id, entry.EventId());
        Assert.Equal(LogEventLevel.Information, entry.Level);
        Assert.Equal(started ? 202 : (int?)null, entry.Value("StatusCode"));
        Assert.Equal("Aborted", entry.Value("Outcome"));
        Assert.Null(entry.Value("ExceptionType"));
        Assert.Null(entry.Exception);
        Assert.Single(await host.Logs.ForTraceAsync((string)entry.Value("TraceId")!, context.TraceIdentifier));
    }

    [Fact]
    public async Task Escaped_failure_after_response_start_preserves_status_and_reports_error()
    {
        await using var host = new PipelineHost(context =>
        {
            context.Response.StatusCode = 202;
            throw new InvalidOperationException("private failure after headers");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Pipeline(host.Context(started: true)));
        var entry = Assert.Single(host.Logs.Entries);
        Assert.Equal(202, entry.Value("StatusCode"));
        Assert.Equal("Failed", entry.Value("Outcome"));
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Serialized_output_uses_safe_request_fields_and_serilog_schema(bool matched)
    {
        await using var host = new PipelineHost(context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BookCatalog.Test");
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["RequestPath"] = "/private-hosting-path",
                ["Scopes"] = new[] { "private scope" }
            });
            using var textScope = logger.BeginScope("private formatted scope");
            logger.LogInformation(LogEvents.BookCreated, "Created book {BookId}.", Guid.Empty);
            return Task.CompletedTask;
        });
        var context = host.Context();
        context.Request.Method = "PRIVATE-METHOD";
        context.Request.Path = "/private-path";
        context.Request.QueryString = new QueryString("?private=query");
        if (!matched) context.SetEndpoint(null);
        await host.Pipeline(context);

        var entries = host.Logs.Entries;
        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.Equal("OTHER", entry.Value("RequestMethod"));
            Assert.Equal(matched ? "api/test/{id}" : "unmatched", entry.Value("Route"));
            Assert.Equal(context.TraceIdentifier, entry.Value("RequestId"));
            Assert.False(entry.Properties.ContainsKey("RequestPath"));
            Assert.False(entry.Properties.ContainsKey("Scope"));
            Assert.False(entry.Properties.ContainsKey("Scopes"));
        });
        var output = host.Logs.JsonOutput();
        Assert.DoesNotContain("private", output, StringComparison.OrdinalIgnoreCase);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var json = JsonDocument.Parse(lines[1]);
        var root = json.RootElement;
        Assert.EndsWith("Z", root.GetProperty("@t").GetString());
        Assert.NotEmpty(root.GetProperty("@m").GetString()!);
        Assert.NotEmpty(root.GetProperty("@i").GetString()!);
        Assert.False(root.TryGetProperty("@l", out _)); // Compact JSON omits Information.
        Assert.False(root.TryGetProperty("@x", out _));
        Assert.Equal(LogEvents.RequestCompleted.Id, root.GetProperty("EventId").GetProperty("Id").GetInt32());
        Assert.Equal(LogEvents.RequestCompleted.Name, root.GetProperty("EventId").GetProperty("Name").GetString());
        Assert.Equal("Serilog.AspNetCore.RequestLoggingMiddleware", root.GetProperty("SourceContext").GetString());
        Assert.True(root.GetProperty("ElapsedMs").GetDouble() >= 0);
        Assert.Equal(root.GetProperty("TraceId").GetString(), root.GetProperty("@tr").GetString());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Default_filters_allow_application_and_lifecycle_events_only(string environment)
    {
        await using var host = new PipelineHost(_ => Task.CompletedTask, environment: environment);
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        factory.CreateLogger("BookCatalog.Test").LogDebug("Hidden debug");
        factory.CreateLogger("BookCatalog.Test").LogInformation("Application event");
        factory.CreateLogger("DatabaseInitialization").LogInformation("Migration event");
        factory.CreateLogger("Microsoft.Hosting.Lifetime").LogInformation("Lifecycle event");
        factory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogCritical("private SQL");
        factory.CreateLogger("System.Test").LogCritical("private system data");
        factory.CreateLogger("Unknown.Test").LogCritical("private unknown data");
        Assert.Equal(3, host.Logs.Entries.Length);
        Assert.DoesNotContain("private", host.Logs.JsonOutput());
    }

    [Fact]
    public async Task Concurrent_hosts_and_requests_have_independent_logging_lifetimes()
    {
        await using var first = new PipelineHost(async context =>
        {
            await Task.Yield();
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BookCatalog.Test")
                .LogInformation("First host event");
        });
        await using var second = new PipelineHost(async context =>
        {
            await Task.Yield();
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BookCatalog.Test")
                .LogInformation("Second host event");
        });
        var staticLogger = Serilog.Log.Logger;
        var firstContext = first.Context();
        var secondContext = second.Context();
        firstContext.Request.Headers["traceparent"] = secondContext.Request.Headers["traceparent"] =
            "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01";
        await Task.WhenAll(first.Pipeline(firstContext), second.Pipeline(secondContext));
        Assert.Equal(2, first.Logs.Entries.Length);
        Assert.Equal(2, second.Logs.Entries.Length);
        Assert.All(first.Logs.Entries, entry => Assert.Equal(firstContext.TraceIdentifier, entry.Value("RequestId")));
        Assert.All(second.Logs.Entries, entry => Assert.Equal(secondContext.TraceIdentifier, entry.Value("RequestId")));
        await first.DisposeAsync();
        await second.Pipeline(second.Context());
        Assert.Equal(4, second.Logs.Entries.Length);
        Assert.Same(staticLogger, Serilog.Log.Logger);
    }

    [Fact]
    public async Task Invalid_startup_configuration_emits_safe_json_and_exits_one()
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add(typeof(BookCatalog.Api.Program).Assembly.Location);
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["Database__ConnectionString"] = "private-invalid-connection-string";
        start.Environment["Database__ApplyMigrationsOnStartup"] = "true";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            Assert.Equal(1, process.ExitCode);
            Assert.DoesNotContain("private-invalid", output + await stderr);
            using var json = JsonDocument.Parse(Assert.Single(output.Split('\n', StringSplitOptions.RemoveEmptyEntries)));
            Assert.Equal("Fatal", json.RootElement.GetProperty("@l").GetString());
            Assert.Equal(LogEvents.ApplicationFailed.Id, json.RootElement.GetProperty("EventId").GetProperty("Id").GetInt32());
            Assert.Contains("Database:ConnectionString", json.RootElement.GetProperty("ConfigurationErrors").GetString());
            Assert.False(json.RootElement.TryGetProperty("@x", out _));
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    // Runs the production logging/exception middleware with a controllable response-start feature.
    // No server or database is needed to test cancellation and exceptions after headers are sent.
    private sealed class PipelineHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private bool _disposed;
        public TestLogSink Logs { get; } = new();
        public IServiceProvider Services => _app.Services;
        public RequestDelegate Pipeline { get; }

        public PipelineHost(RequestDelegate endpoint, bool handleExceptions = false, string environment = "Development")
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environment,
                ApplicationName = typeof(BookCatalog.Api.Program).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddJsonFile("appsettings.json");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<ILogEventSink>(Logs);
            builder.Services.AddCatalogLogging(builder.Configuration);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            _app = builder.Build();
            var pipeline = new ApplicationBuilder(Services);
            pipeline.UseCatalogRequestLogging();
            if (handleExceptions) pipeline.UseExceptionHandler();
            pipeline.Run(endpoint);
            Pipeline = pipeline.Build();
        }

        public DefaultHttpContext Context(bool started = false)
        {
            var context = new DefaultHttpContext { RequestServices = Services, TraceIdentifier = Guid.NewGuid().ToString() };
            context.Features.Set<IHttpResponseFeature>(new ResponseFeature(started));
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
                RoutePatternFactory.Parse("api/test/{id}"), 0, EndpointMetadataCollection.Empty, "test"));
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await _app.DisposeAsync();
        }
    }

    private sealed class ResponseFeature(bool started) : HttpResponseFeature
    {
        public override bool HasStarted => started;
    }
}
