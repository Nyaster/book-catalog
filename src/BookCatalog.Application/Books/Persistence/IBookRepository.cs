using BookCatalog.Domain.Entities;
using BookCatalog.Application.Books.Contracts;

namespace BookCatalog.Application.Books.Persistence;

public interface IBookRepository
{
    Task AddAsync(
        Book book,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Book>> GetPageAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<Book?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Book book,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> IsIsbnInUseAsync(
        string isbn,
        Guid? excludedBookId = null,
        CancellationToken cancellationToken = default);
}