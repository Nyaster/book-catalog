namespace BookCatalog.Application.Common;

public sealed record PageQuery
{
    public int Page { get; }
    public int PageSize { get; }
    public long Offset => ((long)Page - 1) * PageSize;

    public PageQuery(int page = 1, int pageSize = 20)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        Page = page;
        PageSize = pageSize;
    }
}
