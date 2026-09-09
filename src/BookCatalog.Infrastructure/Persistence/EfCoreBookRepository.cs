using BookCatalog.Application.Books.Exceptions;
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

        var filteredBooks = ApplyFilters(_context.Books.AsNoTracking().Include(book => book.Author), pageRequest.Filter);
        var totalCount = await filteredBooks.CountAsync(cancellationToken);
        var offset = ((long)pageRequest.Page - 1) * pageRequest.PageSize;

        IReadOnlyList<Book> books = offset >= totalCount
            ? []
            : await filteredBooks
                .OrderBy(book => book.Title)
                .ThenBy(book => book.Author.Name)
                .ThenBy(book => book.Id)
                .Skip((int)offset)
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

        return _context.Books.Include(book => book.Author).SingleOrDefaultAsync(book => book.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        if (_context.Entry(book).State == EntityState.Detached)
        {
            // Update only catalog fields. A detached book can carry stale availability.
            var affected = await _context.Books.Where(existing => existing.Id == book.Id)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(existing => existing.Title, book.Title)
                    .SetProperty(existing => existing.AuthorId, book.AuthorId)
                    .SetProperty(existing => existing.Isbn, book.Isbn)
                    .SetProperty(existing => existing.PublicationYear, book.PublicationYear)
                    .SetProperty(existing => existing.Description, book.Description), cancellationToken);
            if (affected == 0) throw new BookNotFoundException(book.Id);
            return;
        }

        _context.Entry(book).Property(existing => existing.IsAvailable).IsModified = false;
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
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var translated = PersistenceErrors.Translate(exception);
            _context.Entry(book).State = EntityState.Unchanged;
            if (translated is not null) throw translated;
            throw;
        }

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

    public Task<bool> TryBorrowAsync(Guid id, CancellationToken cancellationToken = default) =>
        TrySetAvailabilityAsync(id, true, false, cancellationToken);

    public Task<bool> TryReleaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        TrySetAvailabilityAsync(id, false, true, cancellationToken);

    private async Task<bool> TrySetAvailabilityAsync(Guid id, bool expected, bool available,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Availability changes must run inside IUnitOfWork.ExecuteAsync.");

        var affected = await _context.Books.Where(book => book.Id == id && book.IsAvailable == expected)
            .ExecuteUpdateAsync(update => update.SetProperty(book => book.IsAvailable, available), cancellationToken);
        if (affected != 1) return false;

        // ExecuteUpdate bypasses tracking. Synchronize availability without scheduling
        // another UPDATE at SaveChanges. Unit-of-work rollback clears the tracker.
        var tracked = _context.Books.Local.FirstOrDefault(book => book.Id == id);
        if (tracked is not null)
        {
            var property = _context.Entry(tracked).Property(book => book.IsAvailable);
            property.CurrentValue = available;
            property.OriginalValue = available;
            property.IsModified = false;
        }
        return true;
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
            books = books.Where(book => EF.Functions.ILike(book.Author.Name, pattern, "\\"));
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