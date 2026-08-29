using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Books.Contracts;
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

    public Task<PagedResult<Book>> GetPageAsync(
        BookListQuery pageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequest);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            var filteredBooks = ApplyFilters(_books.Values, pageRequest.Filter)
                .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => book.Author, StringComparer.OrdinalIgnoreCase)
                .ThenBy(book => book.Id)
                .ToArray();

            var totalCount = filteredBooks.Length;
            var offset = ((long)pageRequest.Page - 1) * pageRequest.PageSize;
            var books = offset >= totalCount
                ? []
                : filteredBooks
                    .Skip((int)offset)
                    .Take(pageRequest.PageSize)
                    .ToArray();

            return Task.FromResult(new PagedResult<Book>(
                books,
                pageRequest.Page,
                pageRequest.PageSize,
                totalCount));
        }
    }

    private static IEnumerable<Book> ApplyFilters(IEnumerable<Book> books, BookFilter filter)
    {
        if (filter.Title is { } title)
        {
            books = books.Where(book =>
                book.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Author is { } author)
        {
            books = books.Where(book =>
                book.Author.Contains(author, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Isbn is { } isbn)
        {
            books = books.Where(book =>
                book.Isbn.Contains(isbn, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.PublicationYear is { } publicationYear)
        {
            books = books.Where(book => book.PublicationYear == publicationYear);
        }

        if (filter.PublicationYearBefore is { } publicationYearBefore)
        {
            books = books.Where(book => book.PublicationYear < publicationYearBefore);
        }

        if (filter.PublicationYearAfter is { } publicationYearAfter)
        {
            books = books.Where(book => book.PublicationYear > publicationYearAfter);
        }

        return books;
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