using System.Net;
using System.Net.Http.Json;
using BookCatalog.Api.Contracts.Books;
using BookCatalog.Api.Contracts.Loans;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.IntegrationTests.Infrastructure;

namespace BookCatalog.IntegrationTests;

public sealed class LoansTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task BorrowReturnAndBorrowAgain_PersistsAvailabilityAndHistory()
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();

        var first = await BorrowAsync(book.Id, user.Id);
        Assert.Equal(book.Id, first.BookId);
        Assert.Equal(book.Title, first.BookTitle);
        Assert.Equal(user.Id, first.UserId);
        Assert.Equal(user.DisplayName, first.UserDisplayName);
        Assert.NotEqual(default, first.BorrowedAt);
        Assert.Null(first.ReturnedAt);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);

        var returned = await ReturnAsync(first.Id, user.Id);
        Assert.Equal(first.Id, returned.Id);
        Assert.Equal(first.BorrowedAt, returned.BorrowedAt);
        Assert.NotNull(returned.ReturnedAt);
        Assert.True(returned.ReturnedAt >= returned.BorrowedAt);
        Assert.Equal(returned, await GetAsync<LoanResponse>($"/api/loans/{first.Id}"));
        Assert.True((await GetBookAsync(book.Id)).IsAvailable);

        var second = await BorrowAsync(book.Id, user.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.False((await GetBookAsync(book.Id)).IsAvailable);
        foreach (var uri in new[] { $"/api/books/{book.Id}/loans", $"/api/users/{user.Id}/loans" })
        {
            var history = await GetAsync<PagedResult<LoanResponse>>(uri);
            Assert.Equal(2, history.TotalCount);
            Assert.Equal(2, history.Items.Count);
            Assert.Contains(returned, history.Items);
            Assert.Contains(second, history.Items);
            Assert.Single(history.Items, loan => loan.ReturnedAt is null);

            var page1 = await GetAsync<PagedResult<LoanResponse>>($"{uri}?pageSize=1&page=1");
            var page2 = await GetAsync<PagedResult<LoanResponse>>($"{uri}?pageSize=1&page=2");
            Assert.Equal(2, page1.TotalCount);
            Assert.Equal(2, page1.TotalPages);
            Assert.Equal(2, page2.TotalCount);
            Assert.NotEqual(Assert.Single(page1.Items).Id, Assert.Single(page2.Items).Id);
            Assert.Equal(history.Items[0], page1.Items[0]);
            Assert.Equal(history.Items[1], page2.Items[0]);
        }
    }

    [Fact]
    public async Task UpdateBorrowedBook_PreservesUnavailableState()
    {
        var book = await CreateBookAsync();
        var user = await CreateUserAsync();
        var loan = await BorrowAsync(book.Id, user.Id);
        using var response = await Client.PutAsJsonAsync($"/api/books/{book.Id}",
            BookRequest(book.AuthorId, book.Isbn, "Updated while borrowed"));

        var updated = await ReadAsync<BookResponse>(response, HttpStatusCode.OK);
        Assert.False(updated.IsAvailable);
        Assert.Equal(updated, await GetBookAsync(book.Id));
        Assert.Equal(loan.Id, Assert.Single((await BookHistoryAsync(book.Id)).Items).Id);
        Assert.Null((await GetAsync<LoanResponse>($"/api/loans/{loan.Id}")).ReturnedAt);
    }
}