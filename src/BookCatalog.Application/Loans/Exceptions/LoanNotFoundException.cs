namespace BookCatalog.Application.Loans.Exceptions;

public sealed class LoanNotFoundException(Guid loanId)
    : Exception($"Loan with ID '{loanId}' was not found.")
{
    public Guid LoanId { get; } = loanId;
}
