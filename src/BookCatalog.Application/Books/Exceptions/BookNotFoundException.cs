namespace BookCatalog.Application.Books.Exceptions;

public sealed class BookNotFoundException : Exception
{
    public BookNotFoundException(Guid bookId)
        : base($"Book with ID '{bookId}' was not found.")
    {
        BookId = bookId;
    }

    public Guid BookId { get; }
}
