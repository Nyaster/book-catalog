using BookCatalog.Application.Authors.Contracts;
using BookCatalog.Application.Authors.Exceptions;
using BookCatalog.Application.Authors.Persistence;
using BookCatalog.Application.Authors.Services;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookCatalog.UnitTests.Application.Authors.Services;

public sealed class AuthorServiceTests
{
    private readonly Mock<IAuthorRepository> _repository = new(MockBehavior.Strict);
    private AuthorService CreateService() => new(_repository.Object, NullLogger<AuthorService>.Instance);

    [Fact]
    public async Task CreateAsync_SavesNormalizedAuthorAndReturnsDto()
    {
        using var cancellation = new CancellationTokenSource();
        Author? saved = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Author>(), cancellation.Token))
            .Callback<Author, CancellationToken>((author, _) => saved = author)
            .Returns(Task.CompletedTask);

        var dto = await CreateService().CreateAsync(new CreateAuthorCommand("  Anne  "), cancellation.Token);

        Assert.NotNull(saved);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal("Anne", dto.Name);
        _repository.Verify(r => r.AddAsync(saved, cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidName_DoesNotWrite(string? name)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => CreateService().CreateAsync(new CreateAuthorCommand(name)));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_WithNullCommand_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService().CreateAsync(null!));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAuthor()
    {
        var author = Author.Create("Anne");
        _repository.Setup(r => r.GetByIdAsync(author.Id, It.IsAny<CancellationToken>())).ReturnsAsync(author);
        var dto = await CreateService().GetByIdAsync(author.Id);
        Assert.Equal(new AuthorDto(author.Id, author.Name), dto);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsAuthorNotFound()
    {
        var id = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Author?)null);
        var error = await Assert.ThrowsAsync<AuthorNotFoundException>(() => CreateService().GetByIdAsync(id));
        Assert.Equal(id, error.AuthorId);
    }

    [Fact]
    public async Task GetPageAsync_ForwardsQueryAndMapsMetadata()
    {
        using var cancellation = new CancellationTokenSource();
        var query = new PageQuery(2, 1);
        var author = Author.Create("Anne");
        _repository.Setup(r => r.GetPageAsync(query, cancellation.Token))
            .ReturnsAsync(new PagedResult<Author>([author], 2, 1, 3));

        var page = await CreateService().GetPageAsync(query, cancellation.Token);

        Assert.Equal(new AuthorDto(author.Id, author.Name), Assert.Single(page.Items));
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
        _repository.Verify(r => r.GetPageAsync(query, cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_WithNullQuery_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateService().GetPageAsync(null!));
        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("create")]
    [InlineData("get")]
    [InlineData("page")]
    public async Task CancelledOperations_DoNotAccessRepository(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "create": await service.CreateAsync(new CreateAuthorCommand("Anne"), cancellation.Token); break;
                case "get": await service.GetByIdAsync(Guid.NewGuid(), cancellation.Token); break;
                case "page": await service.GetPageAsync(new PageQuery(), cancellation.Token); break;
            }
        });

        _repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void PageQuery_RejectsInvalidBounds(int page, int pageSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageQuery(page, pageSize));

    [Fact]
    public void PageQuery_LargePageDoesNotOverflow() =>
        Assert.Equal(214748364600L, new PageQuery(int.MaxValue, 100).Offset);
}
