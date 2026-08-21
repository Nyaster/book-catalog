using BookCatalog.Domain.Entities;

namespace BookCatalog.Application.Books.Persistence;

public interface IBookRepository
{
    Task AddAsync(
        Book book,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Book>> GetAllAsync(
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
