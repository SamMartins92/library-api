using Library.Application.Abstractions;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;

namespace Library.Application.Loans;

public sealed record LoanResponse(
    Guid Id,
    Guid BookId,
    string BookTitle,
    DateTimeOffset BorrowedAt,
    DateTimeOffset DueAt,
    DateTimeOffset? ReturnedAt,
    decimal LateFee,
    bool IsOverdue)
{
    public static LoanResponse From(Loan loan, Book book, DateTimeOffset now) => new(
        loan.Id, book.Id, book.Title, loan.BorrowedAt, loan.DueAt, loan.ReturnedAt, loan.LateFee, loan.IsOverdue(now));
}

public sealed record BorrowBookCommand(Guid BookId);

public sealed class BorrowBook(
    IBookRepository books,
    IMemberRepository members,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<LoanResponse>> HandleAsync(BorrowBookCommand command, CancellationToken cancellationToken)
    {
        var member = await members.GetAsync(currentUser.MemberId, cancellationToken);
        if (member is null)
        {
            return Error.Unauthorized("Member.Unknown", "Your account no longer exists.");
        }

        var book = await books.GetAsync(command.BookId, cancellationToken);
        if (book is null)
        {
            return Error.NotFound("Book.NotFound", "The book does not exist.");
        }

        var now = clock.GetUtcNow();
        var activeLoans = await loans.GetActiveForMemberAsync(member.Id, cancellationToken);

        var loan = LoanPolicy.Borrow(member, book, activeLoans, now);
        if (!loan.IsSuccess)
        {
            return loan.Error!;
        }

        loans.Add(loan.Value);

        // If someone else took the last copy in the meantime, the book's version check turns this into a conflict.
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? LoanResponse.From(loan.Value, book, now) : saved.Error!;
    }
}

public sealed class ReturnBook(
    IBookRepository books,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<LoanResponse>> HandleAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await loans.GetAsync(loanId, cancellationToken);
        if (loan is null || (loan.MemberId != currentUser.MemberId && !currentUser.IsLibrarian))
        {
            // Members cannot see whether other members' loans exist.
            return Error.NotFound("Loan.NotFound", "The loan does not exist.");
        }

        var book = await books.GetAsync(loan.BookId, cancellationToken)
            ?? throw new InvalidOperationException($"Loan {loan.Id} references missing book {loan.BookId}.");

        var now = clock.GetUtcNow();
        var returned = loan.Return(book, now);
        if (!returned.IsSuccess)
        {
            return returned.Error!;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? LoanResponse.From(loan, book, now) : saved.Error!;
    }
}
