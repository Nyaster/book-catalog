using BookCatalog.Application.Users.Contracts;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Application.Users.Persistence;
using BookCatalog.Application.Users.Services;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookCatalog.UnitTests.Application.Users.Services;

public sealed class UserServiceTests
{
    private readonly Mock<IUserRepository> _repository = new(MockBehavior.Strict);
    private UserService CreateService() => new(_repository.Object, NullLogger<UserService>.Instance);

    [Fact]
    public async Task CreateAsync_SavesNormalizedUserAndReturnsDto()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        User? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<User>(), token))
            .Callback<User, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);

        var dto = await CreateService().CreateAsync(new CreateUserCommand("  Anne  "), token);

        Assert.NotNull(saved);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal("Anne", dto.DisplayName);
        _repository.Verify(r => r.AddAsync(saved, token), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidName_DoesNotWrite(string? name)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CreateService().CreateAsync(new CreateUserCommand(name)));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_WithNullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService().CreateAsync(null!));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_WithOverlongDisplayName_DoesNotWrite()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CreateService().CreateAsync(new CreateUserCommand(new string('A', 151))));

        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser()
    {
        var user = User.Create("Anne");
        _repository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var dto = await CreateService().GetByIdAsync(user.Id);
        Assert.Equal(new UserDto(user.Id, user.DisplayName), dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsUserNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var error = await Assert.ThrowsAsync<UserNotFoundException>(() => CreateService().GetByIdAsync(id));
        Assert.Equal(id, error.UserId);
    }

    [Fact]
    public async Task GetPageAsync_ForwardsQueryAndMapsMetadata()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var query = new PageQuery(2, 1);
        var user = User.Create("Anne");
        _repository.Setup(r => r.GetPageAsync(query, token))
            .ReturnsAsync(new PagedResult<User>([user], 2, 1, 3));

        var page = await CreateService().GetPageAsync(query, token);

        Assert.Equal(new UserDto(user.Id, user.DisplayName), Assert.Single(page.Items));
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        _repository.Verify(r => r.GetPageAsync(query, token), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_WithNullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService().GetPageAsync(null!));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPageAsync_WhenNoUsers_ReturnsEmptyPage()
    {
        var query = new PageQuery();
        _repository.Setup(r => r.GetPageAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<User>([], query.Page, query.PageSize, 0));

        var page = await CreateService().GetPageAsync(query);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("get")]
    [InlineData("page")]
    public async Task CancelledOperations_DoNotAccessRepository(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        cancellation.Cancel();
        var service = CreateService();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "create": await service.CreateAsync(new CreateUserCommand("Anne"), token); break;
                case "get": await service.GetByIdAsync(Guid.NewGuid(), token); break;
                case "page": await service.GetPageAsync(new PageQuery(), token); break;
            }
        });

        _repository.VerifyNoOtherCalls();
    }
}