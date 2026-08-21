namespace BookCatalog.Application.Books.Contracts;

public sealed record CreateBookCommand(
    string? Title,
    string? Author,
    string? Isbn,
    int? PublicationYear,
    string? Description);
