using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;
using BookCatalog.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookCatalog.Infrastructure.Persistence;

internal static class PersistenceErrors
{
    internal static bool IsDuplicateIsbn(Exception exception)
    {
       
        var postgres = exception as PostgresException
                       ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return postgres is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Books_Isbn" };
    }

    internal static Exception? Translate(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres) return null;
        if (postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName == LoanConfiguration.ActiveLoanIndex)
            return new DomainConflictException("This book already has an active loan.");

        if (postgres.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation &&
            postgres.ConstraintName == LoanConfiguration.BookForeignKey &&
            exception.Entries.Any(entry => entry.Entity is Book && entry.State == EntityState.Deleted))
            return new DomainConflictException("A book with borrowing history cannot be deleted.");

        if (postgres.SqlState != PostgresErrorCodes.ForeignKeyViolation) return null;
        var loan = exception.Entries.Select(entry => entry.Entity).OfType<Loan>().FirstOrDefault();
        if (loan is not null)
        {
            if (postgres.ConstraintName == LoanConfiguration.BookForeignKey)
                return new BookNotFoundException(loan.BookId);
            if (postgres.ConstraintName == LoanConfiguration.UserForeignKey)
                return new UserNotFoundException(loan.UserId);
        }

        return null;
    }
}