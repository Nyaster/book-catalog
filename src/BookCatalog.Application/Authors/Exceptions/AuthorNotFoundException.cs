namespace BookCatalog.Application.Authors.Exceptions;

public sealed class AuthorNotFoundException(Guid authorId)
    : Exception($"Author with ID '{authorId}' was not found.")
{
    public Guid AuthorId { get; } = authorId;
}
