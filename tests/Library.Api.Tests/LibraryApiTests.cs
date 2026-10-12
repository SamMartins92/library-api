using System.Net;
using System.Net.Http.Json;
using Library.Application.Books;
using Library.Application.Loans;
using Library.Domain.Loans;
using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Tests;

public sealed class LibraryApiTests(LibraryApiFactory factory) : IClassFixture<LibraryApiFactory>
{
    private async Task<BookResponse> AddBookAsync(int copies)
    {
        var librarian = await factory.CreateLibrarianClientAsync();

        var response = await librarian.PostAsJsonAsync("/api/books", new
        {
            isbn = NextIsbn(),
            title = $"Test book {Guid.NewGuid():N}",
            author = "Test Author",
            publishedYear = 2020,
            copies,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookResponse>())!;
    }

    private static int _isbnCounter;

    /// <summary>Generates unique, valid ISBN-13s (979-prefixed test range) so tests do not collide.</summary>
    private static string NextIsbn()
    {
        var body = $"979{Interlocked.Increment(ref _isbnCounter) + 100000000:D9}";
        var sum = body.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return body + (10 - sum % 10) % 10;
    }

    [Fact]
    public async Task SeededCatalogue_CanBeSearched()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var page = await client.GetFromJsonAsync<PagedResult<BookResponse>>("/api/books?query=kleppmann");

        // Assert
        var book = Assert.Single(page!.Items);
        Assert.Equal("Designing Data-Intensive Applications", book.Title);
    }

    [Fact]
    public async Task Search_IsPaged()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var page = await client.GetFromJsonAsync<PagedResult<BookResponse>>("/api/books?pageSize=2&page=1");

        // Assert
        Assert.Equal(2, page!.Items.Count);
        Assert.True(page.TotalCount >= 5);
    }

    [Fact]
    public async Task Member_BorrowsAndReturnsABook()
    {
        // Arrange
        var book = await AddBookAsync(copies: 1);
        var member = await factory.CreateMemberClientAsync();

        // Act
        var borrow = await member.PostAsJsonAsync("/api/loans", new { bookId = book.Id });
        var loan = await borrow.Content.ReadFromJsonAsync<LoanResponse>();
        var afterBorrow = await factory.CreateClient().GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}");
        var returned = await member.PostAsync($"/api/loans/{loan!.Id}/return", null);
        var afterReturn = await factory.CreateClient().GetFromJsonAsync<BookResponse>($"/api/books/{book.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, borrow.StatusCode);
        Assert.Equal(0, afterBorrow!.AvailableCopies);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal(1, afterReturn!.AvailableCopies);
    }

    [Fact]
    public async Task LastCopy_CannotBeBorrowedTwice()
    {
        // Arrange
        var book = await AddBookAsync(copies: 1);
        var first = await factory.CreateMemberClientAsync();
        var second = await factory.CreateMemberClientAsync();
        await first.PostAsJsonAsync("/api/loans", new { bookId = book.Id });

        // Act
        var response = await second.PostAsJsonAsync("/api/loans", new { bookId = book.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Book.Unavailable", problem!.Title);
    }

    [Fact]
    public async Task Member_CannotExceedTheLoanLimit()
    {
        // Arrange
        var member = await factory.CreateMemberClientAsync();
        for (var i = 0; i < LoanPolicy.MaxActiveLoans; i++)
        {
            var book = await AddBookAsync(copies: 1);
            (await member.PostAsJsonAsync("/api/loans", new { bookId = book.Id })).EnsureSuccessStatusCode();
        }

        var oneTooMany = await AddBookAsync(copies: 1);

        // Act
        var response = await member.PostAsJsonAsync("/api/loans", new { bookId = oneTooMany.Id });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LateReturn_IsCharged_AndOverdueMembersCannotBorrow()
    {
        // Arrange
        var email = $"late-{Guid.NewGuid():N}@example.com";
        var member = await factory.CreateMemberClientAsync(email);
        var book = await AddBookAsync(copies: 1);
        var other = await AddBookAsync(copies: 1);
        var loan = await (await member.PostAsJsonAsync("/api/loans", new { bookId = book.Id }))
            .Content.ReadFromJsonAsync<LoanResponse>();
        factory.Clock.Advance(LoanPolicy.LoanPeriod + TimeSpan.FromDays(2));
        member = await factory.CreateClientAsAsync(email, "Password123"); // the old token expired meanwhile

        // Act
        var blocked = await member.PostAsJsonAsync("/api/loans", new { bookId = other.Id });
        var mine = await member.GetFromJsonAsync<List<LoanResponse>>("/api/loans/mine");
        var returned = await (await member.PostAsync($"/api/loans/{loan!.Id}/return", null))
            .Content.ReadFromJsonAsync<LoanResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.True(Assert.Single(mine!, l => l.Id == loan.Id).IsOverdue);
        Assert.Equal(1.00m, returned!.LateFee);
    }

    [Fact]
    public async Task OnlyLibrariansCanAddBooks()
    {
        // Arrange
        var member = await factory.CreateMemberClientAsync();

        // Act
        var asMember = await member.PostAsJsonAsync("/api/books",
            new { isbn = NextIsbn(), title = "X", author = "Y", publishedYear = 2020, copies = 1 });
        var anonymous = await factory.CreateClient().PostAsJsonAsync("/api/books",
            new { isbn = NextIsbn(), title = "X", author = "Y", publishedYear = 2020, copies = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, asMember.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task InvalidRequests_ReturnValidationProblems()
    {
        // Arrange
        var librarian = await factory.CreateLibrarianClientAsync();

        // Act
        var badIsbn = await librarian.PostAsJsonAsync("/api/books",
            new { isbn = "978-0-13-235088-5", title = "X", author = "Y", publishedYear = 2020, copies = 1 });
        var weakPassword = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { name = "Bo", email = "bo@example.com", password = "short" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, badIsbn.StatusCode);
        Assert.Equal("Isbn.CheckDigit", (await badIsbn.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
        Assert.Contains("Password",
            (await weakPassword.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
    }

    [Fact]
    public async Task OpenApiDocument_IsPublished()
    {
        // Act
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Bearer\"", await response.Content.ReadAsStringAsync());
    }
}
