using BookCatalog.Application.Common.Logging;
using Serilog;
using Serilog.Events;

namespace BookCatalog.Api.Logging;

public static class RequestLoggingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCatalogRequestLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestContextMiddleware>();
        return app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            options.MessageTemplate = "HTTP request ended. StatusCode: {StatusCode}; ElapsedMs: {ElapsedMs}; Outcome: {Outcome}; ExceptionType: {ExceptionType}; StackTrace: {StackTrace}";
            options.IncludeQueryInRequestPath = false;
            options.GetLevel = (context, _, exception) =>
            {
                var diagnostics = context.Features.Get<RequestLogContext>()!;
                var aborted = context.RequestAborted.IsCancellationRequested;
                if (exception is not null && !(exception is OperationCanceledException && aborted))
                {
                    diagnostics.EscapedFailure = true;
                    diagnostics.RecordFailure(exception, includeStackTrace: true);
                }

                return aborted ? LogEventLevel.Information
                    : diagnostics.EscapedFailure || context.Response.StatusCode >= 500
                        ? LogEventLevel.Error : LogEventLevel.Information;
            };
            // Replace the default properties entirely: the supplied path is private client input.
            options.GetMessageTemplateProperties = (context, _, elapsedMs, _) =>
            {
                var diagnostics = context.Features.Get<RequestLogContext>()!;
                var aborted = context.RequestAborted.IsCancellationRequested;
                int? status = aborted && !context.Response.HasStarted ? null : context.Response.StatusCode;
                if (diagnostics.EscapedFailure && !context.Response.HasStarted && !aborted)
                    status = StatusCodes.Status500InternalServerError;
                var eventId = aborted ? LogEvents.RequestAborted : LogEvents.RequestCompleted;
                return
                [
                    new LogEventProperty("EventId", new StructureValue(
                    [
                        new LogEventProperty("Id", new ScalarValue(eventId.Id)),
                        new LogEventProperty("Name", new ScalarValue(eventId.Name))
                    ])),
                    new LogEventProperty("StatusCode", new ScalarValue(status)),
                    new LogEventProperty("ElapsedMs", new ScalarValue(elapsedMs)),
                    new LogEventProperty("Outcome", new ScalarValue(aborted ? "Aborted"
                        : diagnostics.EscapedFailure || status >= 500 ? "Failed" : "Completed")),
                    new LogEventProperty("ExceptionType", new ScalarValue(diagnostics.ExceptionType)),
                    new LogEventProperty("StackTrace", new ScalarValue(diagnostics.StackTrace))
                ];
            };
        });
    }
}
