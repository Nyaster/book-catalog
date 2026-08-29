using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BookCatalog.Application.Books.Services;

public sealed class BookService(IBookRepository bookRepository, ILogger<BookService> logger) : IBookService
{
    private readonly IBookRepository _bookRepository =
        bookRepository ?? throw new ArgumentNullException(nameof(bookRepository));

    private readonly ILogger<BookService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<BookDto> CreateAsync(
        CreateBookCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var book = Book.Create(
            command.Title,
            command.Author,
            command.Isbn,
            command.PublicationYear,
            command.Description);

        if (await _bookRepository.IsIsbnInUseAsync(book.Isbn, cancellationToken: cancellationToken))
        {
            throw new DuplicateIsbnException(book.Isbn);
        }

        await _bookRepository.AddAsync(book, cancellationToken);

        _logger.LogInformation("Created book {BookId}.", book.Id);

        return MapToDto(book);
    }

    public async Task<PagedResult<BookDto>> GetPageAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageRequest);
        cancellationToken.ThrowIfCancellationRequested();

        var books = await _bookRepository.GetPageAsync(pageRequest, cancellationToken);

        return new PagedResult<BookDto>(
            books.Items.Select(MapToDto).ToArray(),
            books.Page,
            books.PageSize,
            books.TotalCount);
    }

    public async Task<BookDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var book = await GetRequiredBookAsync(id, cancellationToken);

        return MapToDto(book);
    }

    public async Task<BookDto> UpdateAsync(
        Guid id,
        UpdateBookCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var existingBook = await GetRequiredBookAsync(id, cancellationToken);

        var replacement = Book.Create(
            command.Title,
            command.Author,
            command.Isbn,
            command.PublicationYear,
            command.Description);

        if (await _bookRepository.IsIsbnInUseAsync(
                replacement.Isbn,
                existingBook.Id,
                cancellationToken))
        {
            throw new DuplicateIsbnException(replacement.Isbn);
        }

        existingBook.UpdateDetails(
            command.Title,
            command.Author,
            command.Isbn,
            command.PublicationYear,
            command.Description);

        await _bookRepository.UpdateAsync(existingBook, cancellationToken);

        _logger.LogInformation("Updated book {BookId}.", existingBook.Id);

        return MapToDto(existingBook);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var wasDeleted = await _bookRepository.DeleteAsync(id, cancellationToken);

        if (!wasDeleted)
        {
            throw new BookNotFoundException(id);
        }

        _logger.LogInformation("Deleted book {BookId}.", id);
    }

    private async Task<Book> GetRequiredBookAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var book = await _bookRepository.GetByIdAsync(id, cancellationToken);

        return book ?? throw new BookNotFoundException(id);
    }

    private static BookDto MapToDto(Book book)
    {
        return new BookDto(
            book.Id,
            book.Title,
            book.Author,
            book.Isbn,
            book.PublicationYear,
            book.Description);
    }
}