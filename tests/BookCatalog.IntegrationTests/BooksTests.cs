using System.Net;
using System.Net.Http.Json;
using System.Text;
using BookCatalog.Api.Contracts.Books;
using BookCatalog.IntegrationTests.Infrastructure;

namespace BookCatalog.IntegrationTests;

public sealed class BooksTests(PostgresFixture postgres) : ApiTest(postgres)
{
    [Fact]
    public async Task FreshDatabase_ReturnsEmptyBookList()
    {
        var page = await GetAsync<PagedBooksResponse>("/api/books");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task CreateBook_PersistsFieldsAndMakesBookAvailable()
    {
        var book = await CreateBookAsync();

        Assert.NotEqual(Guid.Empty, book.Id);
        Assert.Equal("Matilda", book.Title);
        Assert.Equal("Test Author", book.Author);
        Assert.NotEqual(Guid.Empty, book.AuthorId);
        Assert.Equal("9780140328721", book.Isbn);
        Assert.Equal(1988, book.PublicationYear);
        Assert.Equal("Integration test book", book.Description);
        Assert.True(book.IsAvailable);
        var page = await GetAsync<PagedBooksResponse>("/api/books");
        Assert.Equal(book, Assert.Single(page.Items));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task UpdateBook_PersistsReplacementFields()
    {
        var book = await CreateBookAsync();
        var author = await CreateAuthorAsync();
        using var response = await Client.PutAsJsonAsync($"/api/books/{book.Id}", new
        {
            title = "Updated title", authorId = author.Id, isbn = "9780142410363",
            publicationYear = 1990, description = "Updated description"
        });

        var updated = await ReadAsync<BookResponse>(response, HttpStatusCode.OK);
        Assert.Equal(book.Id, updated.Id);
        Assert.Equal("Updated title", updated.Title);
        Assert.Equal(author.Id, updated.AuthorId);
        Assert.Equal("9780142410363", updated.Isbn);
        Assert.Equal(1990, updated.PublicationYear);
        Assert.Equal("Updated description", updated.Description);
        Assert.True(updated.IsAvailable);
        Assert.Equal(updated, await GetBookAsync(book.Id));
    }

    [Fact]
    public async Task DeleteBookWithoutHistory_RemovesIt()
    {
        var book = await CreateBookAsync();
        using var deleted = await Client.DeleteAsync($"/api/books/{book.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsStringAsync());

        using var missing = await Client.GetAsync($"/api/books/{book.Id}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateBookWithoutTitle_Returns400AndWritesNothing(string? title)
    {
        var author = await CreateAuthorAsync();
        using var response = await Client.PostAsJsonAsync("/api/books", BookRequest(author.Id, title: title));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Fact]
    public async Task CreateBookWithInvalidIsbn_Returns400AndWritesNothing()
    {
        var author = await CreateAuthorAsync();
        using var response = await Client.PostAsJsonAsync("/api/books", BookRequest(author.Id, "invalid-isbn"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Fact]
    public async Task CreateBookWithMalformedJson_Returns400AndWritesNothing()
    {
        using var content = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/api/books", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Fact]
    public async Task CreateBookWithUnknownAuthor_Returns404AndWritesNothing()
    {
        using var response = await Client.PostAsJsonAsync("/api/books", BookRequest(Guid.NewGuid()));

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task MissingBook_Returns404(string method)
    {
        var author = await CreateAuthorAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/books/{Guid.NewGuid()}");
        if (method == "PUT") request.Content = JsonContent.Create(BookRequest(author.Id));
        using var response = await Client.SendAsync(request);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Empty((await GetAsync<PagedBooksResponse>("/api/books")).Items);
    }

    [Fact]
    public async Task CreateBookWithDuplicateNormalizedIsbn_Returns409AndPreservesOriginal()
    {
        var book = await CreateBookAsync();
        using var response = await Client.PostAsJsonAsync("/api/books",
            BookRequest(book.AuthorId, "978-0-14-032872-1", "Duplicate"));

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        var page = await GetAsync<PagedBooksResponse>("/api/books");
        Assert.Equal(book, Assert.Single(page.Items));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task UpdateBookWithDuplicateIsbn_Returns409AndPreservesBothBooks()
    {
        var first = await CreateBookAsync();
        var second = await CreateBookAsync("9780142410363");
        using var response = await Client.PutAsJsonAsync($"/api/books/{second.Id}",
            BookRequest(second.AuthorId, first.Isbn, "Rejected update"));

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal(first, await GetBookAsync(first.Id));
        Assert.Equal(second, await GetBookAsync(second.Id));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    public async Task InvalidPagination_Returns400(string query)
    {
        using var response = await Client.GetAsync($"/api/books?{query}");
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }
}