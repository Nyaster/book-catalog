using BookCatalog.Application.Books.Persistence;
using BookCatalog.Domain.Entities;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class InMemoryBookRepository : IBookRepository
{
    private readonly Dictionary<Guid, Book> _books = [];
    private readonly Lock _syncRoot = new();

    public Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _books.Add(book.Id, book);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Book>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Book[] books;

        lock (_syncRoot)
        {
            books = _books.Values.ToArray();
        }

        return Task.FromResult<IReadOnlyList<Book>>(books);
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            _books.TryGetValue(id, out var book);

            return Task.FromResult(book);
        }
    }

    public Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!_books.ContainsKey(book.Id))
            {
                throw new KeyNotFoundException($"Book with ID '{book.Id}' was not found.");
            }

            _books[book.Id] = book;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            return Task.FromResult(_books.Remove(id));
        }
    }

    public Task<bool> IsIsbnInUseAsync(
        string isbn,
        Guid? excludedBookId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var isInUse = _books.Values.Any(book =>
                book.Id != excludedBookId &&
                string.Equals(book.Isbn, isbn, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(isInUse);
        }
    }
}