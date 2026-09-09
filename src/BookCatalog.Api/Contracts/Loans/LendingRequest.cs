using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Loans;

public sealed class LendingRequest
{
    [Required(ErrorMessage = "User ID is required.")]
    public Guid? UserId { get; init; }
}
