using BookCatalog.Application.Books.Contracts;

namespace BookCatalog.Api.Contracts.Books;

public static class BookApiMappings
{
    public static CreateBookCommand ToCommand(this CreateBookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateBookCommand(
            request.Title,
            request.Author,
            request.Isbn,
            request.PublicationYear,
            request.Description);
    }

    public static UpdateBookCommand ToCommand(this UpdateBookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateBookCommand(
            request.Title,
            request.Author,
            request.Isbn,
            request.PublicationYear,
            request.Description);
    }

    public static PageRequest ToPageRequest(this GetBooksRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new PageRequest(request.Page, request.PageSize);
    }

    public static BookResponse ToResponse(this BookDto book)
    {
        ArgumentNullException.ThrowIfNull(book);

        return new BookResponse(
            book.Id,
            book.Title,
            book.Author,
            book.Isbn,
            book.PublicationYear,
            book.Description);
    }

    public static PagedBooksResponse ToResponse(this PagedResult<BookDto> books)
    {
        ArgumentNullException.ThrowIfNull(books);

        return new PagedBooksResponse(
            books.Items.Select(book => book.ToResponse()).ToArray(),
            books.Page,
            books.PageSize,
            books.TotalCount,
            books.TotalPages,
            books.HasPreviousPage,
            books.HasNextPage);
    }
}