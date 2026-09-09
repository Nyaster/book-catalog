using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Users;

public sealed class GetUsersRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
