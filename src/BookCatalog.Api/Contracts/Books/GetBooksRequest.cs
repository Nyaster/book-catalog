using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Books;

public sealed class GetBooksRequest
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; init; } = DefaultPage;

    [Range(1, MaxPageSize, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; init; } = DefaultPageSize;
}