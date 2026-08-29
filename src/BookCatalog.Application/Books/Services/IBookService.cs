using BookCatalog.Application.Books.Contracts;

namespace BookCatalog.Application.Books.Services;

public interface IBookService
{
    Task<BookDto> CreateAsync(
        CreateBookCommand command,
        CancellationToken cancellationToken = default);

    Task<PagedResult<BookDto>> GetPageAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<BookDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookDto> UpdateAsync(
        Guid id,
        UpdateBookCommand command,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}