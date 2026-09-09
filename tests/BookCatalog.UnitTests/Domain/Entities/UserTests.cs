using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;

namespace BookCatalog.UnitTests.Domain.Entities;

public sealed class UserTests
{
    [Fact]
    public void Create_TrimsDisplayNameAndAssignsId()
    {
        var user = User.Create("  Łukasz Kowalski  ");

        Assert.Equal("Łukasz Kowalski", user.DisplayName);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WhenDisplayNameIsMissing_Throws(string? displayName)
    {
        var error = Assert.Throws<DomainValidationException>(() => User.Create(displayName));

        Assert.Equal("Display name is required.", error.Message);
    }

    [Fact]
    public void Create_WhenDisplayNameExceeds150Characters_Throws()
    {
        var error = Assert.Throws<DomainValidationException>(() => User.Create(new string('A', 151)));

        Assert.Equal("Display name cannot be longer than 150 characters.", error.Message);
    }

    [Fact]
    public void Create_Accepts150CharactersAfterTrimming()
    {
        var user = User.Create($" {new string('A', 150)} ");

        Assert.Equal(150, user.DisplayName.Length);
    }

    [Fact]
    public void Create_WithSameDisplayName_AssignsDifferentIds()
    {
        var first = User.Create("Alex Smith");
        var second = User.Create("Alex Smith");

        Assert.Equal(first.DisplayName, second.DisplayName);
        Assert.NotEqual(first.Id, second.Id);
    }
}
