namespace BookCatalog.Application.Books.Contracts;

public sealed record CreateBookCommand(
    string? Title,
    Guid AuthorId,
    string? Isbn,
    int? PublicationYear,
    string? Description);
