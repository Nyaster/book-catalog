namespace BookCatalog.Domain.Exceptions;

public sealed class DomainConflictException(string message) : Exception(message);
