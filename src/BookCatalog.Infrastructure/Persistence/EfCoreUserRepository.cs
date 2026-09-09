using BookCatalog.Application.Users.Persistence;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class EfCoreUserRepository(BookCatalogDbContext context) : IUserRepository
{
    private readonly BookCatalogDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<PagedResult<User>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var users = _context.Users.AsNoTracking();
        var totalCount = await users.CountAsync(cancellationToken);
        IReadOnlyList<User> items = query.Offset >= totalCount
            ? []
            : await users.OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
                .Skip((int)query.Offset).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<User>(items, query.Page, query.PageSize, totalCount);
    }
}
