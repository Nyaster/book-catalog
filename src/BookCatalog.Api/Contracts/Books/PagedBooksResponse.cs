namespace BookCatalog.Api.Contracts.Books;

public sealed record PagedBooksResponse(
    IReadOnlyList<BookResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);