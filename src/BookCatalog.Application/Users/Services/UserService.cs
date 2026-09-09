using BookCatalog.Application.Users.Contracts;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Application.Users.Persistence;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BookCatalog.Application.Users.Services;

public sealed class UserService(IUserRepository userRepository, ILogger<UserService> logger) : IUserService
{
    private readonly IUserRepository _userRepository =
        userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    private readonly ILogger<UserService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<UserDto> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var user = User.Create(command.DisplayName);
        await _userRepository.AddAsync(user, cancellationToken);
        _logger.LogInformation("Created user {UserId}.", user.Id);
        return MapToDto(user);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new UserNotFoundException(id);
        return MapToDto(user);
    }

    public async Task<PagedResult<UserDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var users = await _userRepository.GetPageAsync(query, cancellationToken);
        return new PagedResult<UserDto>(users.Items.Select(MapToDto).ToArray(),
            users.Page, users.PageSize, users.TotalCount);
    }

    private static UserDto MapToDto(User user) => new(user.Id, user.DisplayName);
}
