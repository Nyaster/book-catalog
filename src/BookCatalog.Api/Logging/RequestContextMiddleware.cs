using System.Diagnostics;
using Serilog.Context;
using Serilog.Core.Enrichers;

namespace BookCatalog.Api.Logging;

public sealed class RequestContextMiddleware(
    RequestDelegate next,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var ownedActivity = Activity.Current is null ? StartRequestActivity(context) : null;
        var activity = Activity.Current!;
        var diagnostics = new RequestLogContext(activity.TraceId.ToString());
        context.Features.Set(diagnostics);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = diagnostics.TraceId;
            return Task.CompletedTask;
        });

        using var scope = LogContext.Push(
            new PropertyEnricher("Service", environment.ApplicationName),
            new PropertyEnricher("Environment", environment.EnvironmentName),
            new PropertyEnricher("TraceId", diagnostics.TraceId),
            new PropertyEnricher("SpanId", activity.SpanId.ToString()),
            new PropertyEnricher("RequestId", context.TraceIdentifier),
            new PropertyEnricher("RequestMethod", SafeMethod(context.Request.Method)),
            new PropertyEnricher("Route", (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched"));

        await next(context);
    }

    private static Activity StartRequestActivity(HttpContext context)
    {
        var activity = new Activity("BookCatalog.HttpRequest").SetIdFormat(ActivityIdFormat.W3C);
        if (ActivityContext.TryParse(context.Request.Headers["traceparent"].ToString(), null, out var parent))
            activity.SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags);
        return activity.Start();
    }

    // HTTP extension methods are arbitrary client input too.
    private static string SafeMethod(string method) => method switch
    {
        "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS" or "TRACE" or "CONNECT" => method,
        _ => "OTHER"
    };
}
