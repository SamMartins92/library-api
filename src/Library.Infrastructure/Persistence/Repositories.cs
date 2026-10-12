using Library.Application.Abstractions;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

internal sealed class BookRepository(LibraryDbContext db) : IBookRepository
{
    public Task<Book?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Books.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> IsbnExistsAsync(Isbn isbn, CancellationToken cancellationToken) =>
        db.Books.AnyAsync(b => b.Isbn == isbn, cancellationToken);

    public void Add(Book book) => db.Books.Add(book);
}

internal sealed class MemberRepository(LibraryDbContext db) : IMemberRepository
{
    public Task<Member?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Members.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public Task<Member?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Members.FirstOrDefaultAsync(m => m.Email == email, cancellationToken);

    public void Add(Member member) => db.Members.Add(member);
}

internal sealed class LoanRepository(LibraryDbContext db) : ILoanRepository
{
    public Task<Loan?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetActiveForMemberAsync(Guid memberId, CancellationToken cancellationToken) =>
        await db.Loans.Where(l => l.MemberId == memberId && l.ReturnedAt == null).ToListAsync(cancellationToken);

    public void Add(Loan loan) => db.Loans.Add(loan);
}

internal sealed class UnitOfWork(LibraryDbContext db) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("Concurrency", "The data changed while your request was processed. Please try again.");
        }
    }
}
