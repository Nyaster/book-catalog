using BookCatalog.Application.Loans.Services;
using BookCatalog.Application.Users.Services;
using BookCatalog.Application.Authors.Services;
using BookCatalog.Application.Books.Services;
using BookCatalog.Api.ErrorHandling;
using BookCatalog.Api.Logging;
using BookCatalog.Api.Health;
using BookCatalog.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi;

namespace BookCatalog.Api;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            await RunAsync(args);
            return 0;
        }
        catch (Exception exception) when (exception is not HostAbortedException)
        {
            ApplicationFailureLogging.Log(exception);
            return 1;
        }
    }

    private static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.AddCatalogLogging(builder.Configuration);
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
        builder.Services.AddHealthChecks()
            .AddCheck<ReadinessHealthCheck>("ready", timeout: TimeSpan.FromSeconds(3));

        builder.Services.AddControllers(options =>
        {
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        });
        builder.Services.AddValidation();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = RequestLogContext.TraceIdFor(context.HttpContext));
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Book Catalog API",
                    Version = "v1",
                    Description = "A REST API for book catalog."
                };

                return Task.CompletedTask;
            });
        });
        builder.Services.AddScoped<IBookService, BookService>();
        builder.Services.AddScoped<IAuthorService, AuthorService>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ILendingService, LendingService>();
        builder.Services.AddSingleton(TimeProvider.System);

        await using var app = builder.Build();

        app.UseRouting();
        app.UseCatalogRequestLogging();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Book Catalog API v1");
                options.DocumentTitle = "Book Catalog API";
            });
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseWhen(context => context.Request.Path != "/health/live"
                                   && context.Request.Path != "/health/ready",
                branch => branch.UseHttpsRedirection());
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == "ready" });
        app.MapControllers();
        await app.Services.ApplyMigrationsAsync(app.Lifetime.ApplicationStopped);
        await app.RunAsync();
    }
}