using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;

namespace BookCatalog.Application.Authors.Persistence;

public interface IAuthorRepository
{
    Task AddAsync(Author author, CancellationToken cancellationToken = default);
    Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Author>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default);
}
