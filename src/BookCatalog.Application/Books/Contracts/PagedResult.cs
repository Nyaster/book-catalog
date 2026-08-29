namespace BookCatalog.Application.Books.Contracts;

public sealed record PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Page must be at least 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be at least 1.");
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => (int)((TotalCount + (long)PageSize - 1) / PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}