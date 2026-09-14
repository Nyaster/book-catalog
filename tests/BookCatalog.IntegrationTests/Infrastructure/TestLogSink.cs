using BookCatalog.Application.Common.Logging;
using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BookCatalog.IntegrationTests.Infrastructure;

// Each host registers its own sink, downstream of the production sanitization step.
internal sealed class TestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _entries = new();
    public LogEvent[] Entries => _entries.ToArray();
    public void Emit(LogEvent logEvent) => _entries.Enqueue(logEvent);

    public async Task<LogEvent[]> ForTraceAsync(string traceId, string? requestId = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            while (true)
            {
                var entries = Entries.Where(entry => Equals((entry.Properties.GetValueOrDefault("TraceId") as ScalarValue)?.Value, traceId)
                    && (requestId is null || Equals((entry.Properties.GetValueOrDefault("RequestId") as ScalarValue)?.Value, requestId))).ToArray();
                // The client can receive its response just before the terminal event is written.
                if (entries.Any(entry => entry.EventId() == LogEvents.RequestCompleted.Id
                                         || entry.EventId() == LogEvents.RequestAborted.Id))
                    return entries;
                await Task.Delay(10, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"No terminal log for trace {traceId}, request {requestId ?? "any"} within five seconds.");
        }
    }

    public string JsonOutput()
    {
        using var writer = new StringWriter();
        var formatter = new RenderedCompactJsonFormatter();
        foreach (var entry in Entries)
            formatter.Format(entry, writer);
        return writer.ToString();
    }
}

internal static class LogEventAssertions
{
    public static object? Value(this LogEvent entry, string property) =>
        Assert.IsType<ScalarValue>(entry.Properties[property]).Value;

    public static int? EventId(this LogEvent entry) =>
        entry.Properties.TryGetValue("EventId", out var value) && value is StructureValue structure
            && structure.Properties.FirstOrDefault(property => property.Name == "Id")?.Value is ScalarValue { Value: int id }
                ? id : null;
}
