using System.Globalization;
using Dapper;
using Library.Application.Abstractions;
using Library.Application.Books;
using Library.Application.Loans;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Read side: hand-written SQL through Dapper on the same connection EF Core uses.
/// Rows are read as SQLite stores them (TEXT ids, dates and decimals) and mapped explicitly.
/// </summary>
internal sealed class BookQueries(LibraryDbContext db) : IBookQueries
{
    private const string Columns = "Id, Isbn, Title, Author, PublishedYear, TotalCopies, AvailableCopies";

    public async Task<PagedResult<BookResponse>> SearchAsync(BookSearch search, CancellationToken cancellationToken)
    {
        const string filter = """
            WHERE (@Pattern IS NULL OR Title LIKE @Pattern OR Author LIKE @Pattern OR Isbn LIKE @Pattern)
              AND (@AvailableOnly = 0 OR AvailableCopies > 0)
            """;

        var parameters = new
        {
            Pattern = string.IsNullOrWhiteSpace(search.Query) ? null : $"%{search.Query.Trim()}%",
            search.AvailableOnly,
            search.PageSize,
            Offset = (search.Page - 1) * search.PageSize,
        };

        var connection = db.Database.GetDbConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM Books {filter}", parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<BookRow>(new CommandDefinition(
            $"SELECT {Columns} FROM Books {filter} ORDER BY Title LIMIT @PageSize OFFSET @Offset",
            parameters, cancellationToken: cancellationToken));

        return new PagedResult<BookResponse>([.. rows.Select(r => r.ToResponse())], search.Page, search.PageSize, total);
    }

    public async Task<BookResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Database.GetDbConnection().QuerySingleOrDefaultAsync<BookRow>(new CommandDefinition(
            $"SELECT {Columns} FROM Books WHERE Id = @Id", new { Id = SqliteValues.Id(id) },
            cancellationToken: cancellationToken));
        return row?.ToResponse();
    }

    private sealed record BookRow(
        string Id, string Isbn, string Title, string Author, long PublishedYear, long TotalCopies, long AvailableCopies)
    {
        public BookResponse ToResponse() =>
            new(Guid.Parse(Id), Isbn, Title, Author, (int)PublishedYear, (int)TotalCopies, (int)AvailableCopies);
    }
}

internal sealed class LoanQueries(LibraryDbContext db) : ILoanQueries
{
    public async Task<IReadOnlyList<LoanResponse>> ForMemberAsync(Guid memberId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT l.Id, l.BookId, b.Title AS BookTitle, l.BorrowedAt, l.DueAt, l.ReturnedAt, l.LateFee
            FROM Loans l
            JOIN Books b ON b.Id = l.BookId
            WHERE l.MemberId = @MemberId
            ORDER BY l.ReturnedAt IS NOT NULL, l.DueAt
            """;

        var rows = await db.Database.GetDbConnection().QueryAsync<LoanRow>(new CommandDefinition(
            sql, new { MemberId = SqliteValues.Id(memberId) }, cancellationToken: cancellationToken));

        return [.. rows.Select(r => r.ToResponse(now))];
    }

    private sealed record LoanRow(
        string Id, string BookId, string BookTitle, string BorrowedAt, string DueAt, string? ReturnedAt, string LateFee)
    {
        public LoanResponse ToResponse(DateTimeOffset now)
        {
            var dueAt = SqliteValues.Date(DueAt);
            var returnedAt = ReturnedAt is null ? (DateTimeOffset?)null : SqliteValues.Date(ReturnedAt);
            return new LoanResponse(Guid.Parse(Id), Guid.Parse(BookId), BookTitle, SqliteValues.Date(BorrowedAt), dueAt,
                returnedAt, decimal.Parse(LateFee, CultureInfo.InvariantCulture), returnedAt is null && now > dueAt);
        }
    }
}

internal static class SqliteValues
{
    /// <summary>EF Core's SQLite provider stores GUIDs as upper-case TEXT.</summary>
    public static string Id(Guid id) => id.ToString().ToUpperInvariant();

    public static DateTimeOffset Date(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
