using BookCatalog.Application.Authors.Persistence;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class EfCoreAuthorRepository(BookCatalogDbContext context) : IAuthorRepository
{
    private readonly BookCatalogDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task AddAsync(Author author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(author);
        cancellationToken.ThrowIfCancellationRequested();
        _context.Authors.Add(author);
        await _context.SaveChangesAsync(cancellationToken);
    }
    public Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Authors.SingleOrDefaultAsync(author => author.Id == id, cancellationToken);

    public async Task<PagedResult<Author>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var authors = _context.Authors.AsNoTracking();
        var totalCount = await authors.CountAsync(cancellationToken);
        IReadOnlyList<Author> items = query.Offset >= totalCount
            ? []
            : await authors.OrderBy(author => author.Name).ThenBy(author => author.Id)
                .Skip((int)query.Offset).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<Author>(items, query.Page, query.PageSize, totalCount);
    }
}
