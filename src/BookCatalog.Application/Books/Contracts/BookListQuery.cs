namespace BookCatalog.Application.Books.Contracts;

public sealed record BookListQuery
{
    public BookListQuery(int page, int pageSize, BookFilter? filter = null)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be at least 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be at least 1.");
        }

        Page = page;
        PageSize = pageSize;
        Filter = filter ?? BookFilter.Empty;
    }

    public int Page { get; }

    public int PageSize { get; }

    public BookFilter Filter { get; }
}
