namespace BookCatalog.Application.Books.Contracts;

public sealed record UpdateBookCommand(
    string? Title,
    string? Author,
    string? Isbn,
    int? PublicationYear,
    string? Description);
