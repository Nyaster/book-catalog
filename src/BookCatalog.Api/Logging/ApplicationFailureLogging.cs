using BookCatalog.Application.Common.Logging;
using System.Diagnostics;
using BookCatalog.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;

namespace BookCatalog.Api.Logging;

internal static class ApplicationFailureLogging
{
    public static void Log(Exception exception)
    {
        // Independent of host configuration and lifetime, including failures before DI is available.
        using var logger = new LoggerConfiguration()
            .Enrich.WithProperty("SourceContext", "BookCatalog.Api.Program")
            .Enrich.WithProperty("EventId", new { LogEvents.ApplicationFailed.Id, LogEvents.ApplicationFailed.Name }, true)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();
        // This validator produces fixed descriptions of configuration keys, never their values.
        var configurationErrors = exception is OptionsValidationException validation
                                  && validation.OptionsType == typeof(DatabaseOptions)
            ? string.Join(" ", validation.Failures)
            : null;
        logger.Fatal(
            "Application could not continue. ExceptionType: {ExceptionType}; StackTrace: {StackTrace}; ConfigurationErrors: {ConfigurationErrors}",
            exception.GetType().FullName, new StackTrace(exception, false).ToString(), configurationErrors);
    }
}
