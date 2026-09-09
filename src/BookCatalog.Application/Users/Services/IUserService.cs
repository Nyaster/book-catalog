using BookCatalog.Application.Users.Contracts;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;

namespace BookCatalog.Application.Users.Services;

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default);
}
