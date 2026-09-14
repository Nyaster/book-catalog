using BookCatalog.Application.Loans.Exceptions;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Application.Authors.Exceptions;
using System.Text.Json;
using BookCatalog.Api.Logging;
using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Domain.Exceptions;
using BookCatalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            _ when DatabaseFailures.IsTransient(exception) => CreateProblemDetails(
                StatusCodes.Status503ServiceUnavailable,
                "Service temporarily unavailable",
                "The database is temporarily unavailable."),
            DomainValidationException => CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                exception.Message),
            DomainConflictException => CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "Operation conflicts with current state",
                exception.Message),
            LoanNotFoundException => CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Loan not found",
                exception.Message),
            UserNotFoundException => CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "User not found",
                exception.Message),
            AuthorNotFoundException => CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Author not found",
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

        var statusCode = problemDetails.Status!.Value;

        httpContext.Features.Get<RequestLogContext>()?.RecordFailure(exception,
            includeStackTrace: statusCode == StatusCodes.Status500InternalServerError);

        httpContext.Response.StatusCode = statusCode;

        var wasWritten = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (wasWritten) return true;
        problemDetails.Extensions["traceId"] = RequestLogContext.TraceIdFor(httpContext);

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            JsonSerializerOptions.Web,
            "application/problem+json",
            cancellationToken);

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