using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BookCatalog.Api.Logging;

public static class LoggingServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSerilog((provider, logging) =>
        {
            // Only levels are configurable; all destinations must receive sanitized events.
            var levels = configuration.GetSection("Serilog:MinimumLevel");
            logging.MinimumLevel.Is(Enum.Parse<LogEventLevel>(levels["Default"] ?? "Information", true));
            foreach (var category in levels.GetSection("Override").GetChildren())
                logging.MinimumLevel.Override(category.Key, Enum.Parse<LogEventLevel>(category.Value!, true));

            logging.Enrich.FromLogContext()
                .Filter.ByIncludingOnly(IsAllowedSource)
                .WriteTo.Sink(LoggerSinkConfiguration.Wrap(inner => new SanitizingLogSink(inner), sinks =>
                {
                    sinks.Console(new RenderedCompactJsonFormatter());
                    foreach (var sink in provider.GetServices<ILogEventSink>())
                        sinks.Sink(sink);
                }));
        }, preserveStaticLogger: true);
        return services;
    }

    private static bool IsAllowedSource(LogEvent entry)
    {
        var source = (entry.Properties.GetValueOrDefault("SourceContext") as ScalarValue)?.Value as string;
        return source is "BookCatalog" or "DatabaseInitialization" or "Microsoft.Hosting.Lifetime"
                   or "Serilog.AspNetCore.RequestLoggingMiddleware"
               || source?.StartsWith("BookCatalog.", StringComparison.Ordinal) == true;
    }
}
