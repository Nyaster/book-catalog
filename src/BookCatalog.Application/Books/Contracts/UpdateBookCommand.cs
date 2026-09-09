namespace BookCatalog.Application.Books.Contracts;

public sealed record UpdateBookCommand(
    string? Title,
    Guid AuthorId,
    string? Isbn,
    int? PublicationYear,
    string? Description);
