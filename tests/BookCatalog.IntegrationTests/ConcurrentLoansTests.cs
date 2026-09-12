using System.Net;
using System.Net.Http.Json;
using BookCatalog.Api.Contracts.Loans;
using BookCatalog.IntegrationTests.Infrastructure;

namespace BookCatalog.IntegrationTests;

public sealed class ConcurrentLoansTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task CompetingBorrows_OnlyOneSucceedsAndCreatesAnActiveLoan()
    {
        var book = await CreateBookAsync();
        var first = await CreateUserAsync("First borrower");
        var second = await CreateUserAsync("Second borrower");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstRequest = BorrowWhenReleasedAsync(first.Id);
        var secondRequest = BorrowWhenReleasedAsync(second.Id);
        gate.SetResult();
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
        var loan = await ReadAsync<LoanResponse>(success, HttpStatusCode.Created);
        Assert.Contains(loan.UserId, new[] { first.Id, second.Id });
        Assert.Equal(book.Id, loan.BookId);
        Assert.Null(loan.ReturnedAt);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        var history = await BookHistoryAsync(book.Id);
        Assert.Equal(1, history.TotalCount);
        AssertSameLoan(loan, Assert.Single(history.Items));

        async Task<HttpResponseMessage> BorrowWhenReleasedAsync(Guid userId)
        {
            await gate.Task;
            return await Client.PostAsJsonAsync($"/api/books/{book.Id}/loans", new { userId });
        }
    }

    [Fact]
    public async Task CompetingReturns_OnlyOneSucceedsAndReleasesTheBook()
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstRequest = ReturnWhenReleasedAsync();
        var secondRequest = ReturnWhenReleasedAsync();
        gate.SetResult();
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict);
        var returned = await ReadAsync<LoanResponse>(success, HttpStatusCode.OK);
        Assert.Equal(loan.Id, returned.Id);
        Assert.NotNull(returned.ReturnedAt);
        Assert.True(returned.ReturnedAt >= loan.BorrowedAt);
        Assert.True((await GetBookAsync(book.Id)).IsAvailable);
        var history = await BookHistoryAsync(book.Id);
        Assert.Equal(1, history.TotalCount);
        AssertSameLoan(returned, Assert.Single(history.Items));

        async Task<HttpResponseMessage> ReturnWhenReleasedAsync()
        {
            await gate.Task;
            return await Client.PostAsJsonAsync($"/api/loans/{loan.Id}/return", new { userId = user.Id });
        }
    }
}