namespace BookCatalog.Application.Books.Exceptions;

public sealed class DuplicateIsbnException : Exception
{
    public DuplicateIsbnException(string isbn)
        : base($"A book with ISBN '{isbn}' already exists.")
    {
        Isbn = isbn;
    }

    public string Isbn { get; }
}
