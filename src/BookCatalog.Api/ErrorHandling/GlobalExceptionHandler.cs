using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            DomainValidationException => CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Book validation failed",
                exception.Message),
            BookNotFoundException => CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Book not found",
                exception.Message),
            DuplicateIsbnException => CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "ISBN already exists",
                exception.Message),
            _ => CreateProblemDetails(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The server encountered an unexpected error.")
        };

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        return true;
    }

    private static ProblemDetails CreateProblemDetails(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
