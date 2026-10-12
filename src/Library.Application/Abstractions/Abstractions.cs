using Library.Application.Books;
using Library.Application.Loans;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Members;

namespace Library.Application.Abstractions;

// Write side: aggregates loaded and saved through EF Core.

public interface IBookRepository
{
    Task<Book?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IsbnExistsAsync(Isbn isbn, CancellationToken cancellationToken);

    void Add(Book book);
}

public interface IMemberRepository
{
    Task<Member?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    void Add(Member member);
}

public interface ILoanRepository
{
    Task<Loan?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Loan>> GetActiveForMemberAsync(Guid memberId, CancellationToken cancellationToken);

    void Add(Loan loan);
}

public interface IUnitOfWork
{
    /// <summary>Commits pending changes; returns a conflict when another request changed the same data first.</summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}

// Read side: plain SQL (Dapper) projected straight into response models.

public interface IBookQueries
{
    Task<PagedResult<BookResponse>> SearchAsync(BookSearch search, CancellationToken cancellationToken);

    Task<BookResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface ILoanQueries
{
    Task<IReadOnlyList<LoanResponse>> ForMemberAsync(Guid memberId, DateTimeOffset now, CancellationToken cancellationToken);
}

// Cross-cutting services implemented by the infrastructure / API layers.

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken Issue(Member member);
}

public interface ICurrentUser
{
    Guid MemberId { get; }

    bool IsLibrarian { get; }
}
