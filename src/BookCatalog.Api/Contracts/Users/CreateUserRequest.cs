using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Users;

public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Display name is required.")]
    [StringLength(150, ErrorMessage = "Display name cannot be longer than 150 characters.")]
    public string? DisplayName { get; init; }
}
