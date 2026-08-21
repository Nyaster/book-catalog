namespace BookCatalog.Application.Books.Contracts;

public sealed record BookDto(
    Guid Id,
    string Title,
    string Author,
    string Isbn,
    int PublicationYear,
    string? Description);
