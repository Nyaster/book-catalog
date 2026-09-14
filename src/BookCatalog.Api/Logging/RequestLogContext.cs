using System.Diagnostics;

namespace BookCatalog.Api.Logging;

internal sealed class RequestLogContext(string traceId)
{
    public string TraceId { get; } = traceId;
    public bool EscapedFailure { get; set; }
    public string? ExceptionType { get; private set; }
    public string? StackTrace { get; private set; }

    public void RecordFailure(Exception exception, bool includeStackTrace)
    {
        ExceptionType = exception.GetType().FullName;
        // Never serialize the exception, its message, Data, or inner exceptions.
        StackTrace = includeStackTrace ? new StackTrace(exception, false).ToString() : null;
    }

    public static string TraceIdFor(HttpContext context) =>
        context.Features.Get<RequestLogContext>()?.TraceId
        ?? Activity.Current?.TraceId.ToString()
        ?? context.TraceIdentifier;
}
