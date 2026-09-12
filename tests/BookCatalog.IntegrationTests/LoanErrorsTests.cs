using System.Net;
using System.Net.Http.Json;
using BookCatalog.Api.Contracts.Loans;
using BookCatalog.IntegrationTests.Infrastructure;

namespace BookCatalog.IntegrationTests;

public sealed class LoanErrorsTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task BorrowUnavailableBook_Returns409AndPreservesActiveLoan()
    {
        var book = await CreateBookAsync();
        var firstUser = await CreateUserAsync();
        var otherUser = await CreateUserAsync("Other borrower");
        var loan = await BorrowAsync(book.Id, firstUser.Id);
        using var response = await Client.PostAsJsonAsync($"/api/books/{book.Id}/loans", new { userId = otherUser.Id });

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Equal(loan, Assert.Single((await BookHistoryAsync(book.Id)).Items));
    }

    [Fact]
    public async Task ReturnByDifferentUser_Returns409AndPreservesActiveLoan()
    {
        var book = await CreateBookAsync();
        var borrower = await CreateUserAsync();
        var other = await CreateUserAsync("Other borrower");
        var loan = await BorrowAsync(book.Id, borrower.Id);
        using var response = await Client.PostAsJsonAsync($"/api/loans/{loan.Id}/return", new { userId = other.Id });

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Equal(loan, await GetAsync<LoanResponse>($"/api/loans/{loan.Id}"));
        Assert.Equal(loan, Assert.Single((await BookHistoryAsync(book.Id)).Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedReturn_Returns409AndNeverReleasesNewLoan(bool borrowAgain)
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var first = await BorrowAsync(book.Id, user.Id);
        var returned = await ReturnAsync(first.Id, user.Id);
        var second = borrowAgain ? await BorrowAsync(book.Id, user.Id) : null;

        using var response = await Client.PostAsJsonAsync($"/api/loans/{first.Id}/return", new { userId = user.Id });

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(!borrowAgain, (await GetBookAsync(book.Id)).IsAvailable);
        Assert.Equal(returned, await GetAsync<LoanResponse>($"/api/loans/{first.Id}"));
        var history = await BookHistoryAsync(book.Id);
        Assert.Equal(borrowAgain ? 2 : 1, history.TotalCount);
        Assert.Contains(returned, history.Items);
        if (second is not null)
            Assert.Equal(second, Assert.Single(history.Items, loan => loan.ReturnedAt is null));
        else
            Assert.Equal(returned, Assert.Single(history.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteBookWithHistory_Returns409AndPreservesHistory(bool returnFirst)
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        if (returnFirst) loan = await ReturnAsync(loan.Id, user.Id);
        using var response = await Client.DeleteAsync($"/api/books/{book.Id}");

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(returnFirst, (await GetBookAsync(book.Id)).IsAvailable);
        Assert.Equal(loan, Assert.Single((await BookHistoryAsync(book.Id)).Items));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task BorrowWithMissingOrEmptyUserId_Returns400AndWritesNothing(string json)
    {
        var book = await CreateBookAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync($"/api/books/{book.Id}/loans", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Empty((await BookHistoryAsync(book.Id)).Items);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"userId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task ReturnWithMissingOrEmptyUserId_Returns400AndPreservesActiveLoan(string json)
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync($"/api/loans/{loan.Id}/return", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Equal(loan, Assert.Single((await BookHistoryAsync(book.Id)).Items));
    }

    [Fact]
    public async Task BorrowWithMissingUser_Returns404AndWritesNothing()
    {
        var book = await CreateBookAsync();
        using var response =
            await Client.PostAsJsonAsync($"/api/books/{book.Id}/loans", new { userId = Guid.NewGuid() });

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.True((await GetBookAsync(book.Id)).IsAvailable);
        Assert.Empty((await BookHistoryAsync(book.Id)).Items);
    }

    [Theory]
    [InlineData("books", "loans", "POST")]
    [InlineData("books", "loans", "GET")]
    [InlineData("users", "loans", "GET")]
    [InlineData("loans", "", "GET")]
    [InlineData("loans", "return", "POST")]
    public async Task MissingLendingResource_Returns404(string resource, string action, string method)
    {
        var user = await CreateUserAsync();
        using var request =
            new HttpRequestMessage(new HttpMethod(method), $"/api/{resource}/{Guid.NewGuid()}/{action}");
        if (method == "POST") request.Content = JsonContent.Create(new { userId = user.Id });
        using var response = await Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }
}