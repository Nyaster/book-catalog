namespace BookCatalog.Domain.Exceptions;

public class DomainValidationException(string message) : Exception(message);