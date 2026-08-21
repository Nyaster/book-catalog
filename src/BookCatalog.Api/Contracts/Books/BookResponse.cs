namespace BookCatalog.Api.Contracts.Books;

public sealed record BookResponse(
    Guid Id,
    string Title,
    string Author,
    string Isbn,
    int PublicationYear,
    string? Description);
