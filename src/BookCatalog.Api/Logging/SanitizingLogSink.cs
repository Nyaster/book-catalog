using Serilog.Core;
using Serilog.Events;

namespace BookCatalog.Api.Logging;

// Serilog attaches escaped exceptions and hosting can contribute scopes containing raw URLs.
// Sanitize before fan-out so every destination, including test sinks, sees the same event.
internal sealed class SanitizingLogSink(ILogEventSink inner) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var properties = logEvent.Properties
            .Where(property => property.Key is not ("RequestPath" or "Scope" or "Scopes"))
            .Select(property => new LogEventProperty(property.Key, property.Value));
        inner.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, null, logEvent.MessageTemplate,
            properties, logEvent.TraceId ?? default, logEvent.SpanId ?? default));
    }
}
