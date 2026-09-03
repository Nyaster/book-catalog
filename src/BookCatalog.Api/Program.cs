using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Books.Services;
using BookCatalog.Api.ErrorHandling;
using BookCatalog.Infrastructure;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.OpenApi;

namespace BookCatalog.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers(options =>
        {
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        });
        builder.Services.AddValidation();
        builder.Services.AddProblemDetails();
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
        builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();
        builder.Services.AddScoped<IBookService, BookService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Book Catalog API v1");
                options.DocumentTitle = "Book Catalog API";
            });
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();

        app.MapControllers();

        app.Run();
    }
}