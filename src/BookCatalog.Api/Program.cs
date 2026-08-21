using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Books.Services;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

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
        builder.Services.AddOpenApi();
        builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();
        builder.Services.AddScoped<IBookService, BookService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.MapControllers();

        app.Run();
    }
}