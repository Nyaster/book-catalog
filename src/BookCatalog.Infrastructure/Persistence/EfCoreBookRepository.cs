using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class EfCoreBookRepository(BookCatalogDbContext context) : IBookRepository
{
    private readonly BookCatalogDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));

    public async Task AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        await _context.Books.AddAsync(book, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Book>> GetPageAsync(
        BookListQuery pageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequest);
        cancellationToken.ThrowIfCancellationRequested();

        var filteredBooks = ApplyFilters(_context.Books.AsNoTracking(), pageRequest.Filter);
        var totalCount = await filteredBooks.CountAsync(cancellationToken);
        var offset = (pageRequest.Page - 1) * pageRequest.PageSize;

        IReadOnlyList<Book> books = offset >= totalCount
            ? []
            : await filteredBooks
                .OrderBy(book => book.Title)
                .ThenBy(book => book.Author)
                .ThenBy(book => book.Id)
                .Skip(offset)
                .Take(pageRequest.PageSize)
                .ToListAsync(cancellationToken);

        return new PagedResult<Book>(
            books,
            pageRequest.Page,
            pageRequest.PageSize,
            totalCount);
    }

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _context.Books.SingleOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        if (_context.Entry(book).State == EntityState.Detached)
        {
            var exists = await _context.Books
                .AsNoTracking()
                .AnyAsync(existingBook => existingBook.Id == book.Id, cancellationToken);

            if (!exists)
            {
                throw new KeyNotFoundException($"Book with ID '{book.Id}' was not found.");
            }

            _context.Books.Update(book);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await _context.Books.SingleOrDefaultAsync(
            book => book.Id == id,
            cancellationToken);

        if (book is null)
        {
            return false;
        }

        _context.Books.Remove(book);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public Task<bool> IsIsbnInUseAsync(
        string isbn,
        Guid? excludedBookId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedIsbn = isbn.ToUpperInvariant();

        return _context.Books
            .AsNoTracking()
            .AnyAsync(
                book => book.Id != excludedBookId && book.Isbn == normalizedIsbn,
                cancellationToken);
    }

    private static IQueryable<Book> ApplyFilters(IQueryable<Book> books, BookFilter filter)
    {
        if (filter.Title is { } title)
        {
            var pattern = CreateContainsPattern(title);
            books = books.Where(book => EF.Functions.ILike(book.Title, pattern, "\\"));
        }

        if (filter.Author is { } author)
        {
            var pattern = CreateContainsPattern(author);
            books = books.Where(book => EF.Functions.ILike(book.Author, pattern, "\\"));
        }

        if (filter.Isbn is { } isbn)
        {
            var pattern = CreateContainsPattern(isbn);
            books = books.Where(book => EF.Functions.ILike(book.Isbn, pattern, "\\"));
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

    private static string CreateContainsPattern(string value)
    {
        var escapedValue = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escapedValue}%";
    }
}