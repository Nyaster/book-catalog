namespace BookCatalog.Application.Users.Exceptions;

public sealed class UserNotFoundException(Guid userId)
    : Exception($"User with ID '{userId}' was not found.")
{
    public Guid UserId { get; } = userId;
}
