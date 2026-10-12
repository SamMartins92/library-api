using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Members;

namespace Library.Domain.Loans;

/// <summary>The library's lending rules, kept in one place so they can be read (and tested) together.</summary>
public static class LoanPolicy
{
    public const int MaxActiveLoans = 3;
    public static readonly TimeSpan LoanPeriod = TimeSpan.FromDays(14);
    public const decimal DailyLateFee = 0.50m;

    /// <param name="memberLoans">The member's active loans.</param>
    public static Result<Loan> Borrow(Member member, Book book, IReadOnlyCollection<Loan> memberLoans, DateTimeOffset now)
    {
        if (memberLoans.Any(l => l.IsOverdue(now)))
        {
            return Error.Forbidden("Loan.Overdue", "Return your overdue books before borrowing another one.");
        }

        if (memberLoans.Count(l => l.IsActive) >= MaxActiveLoans)
        {
            return Error.Conflict("Loan.Limit", $"A member can have at most {MaxActiveLoans} books at a time.");
        }

        if (memberLoans.Any(l => l.IsActive && l.BookId == book.Id))
        {
            return Error.Conflict("Loan.Duplicate", "You already have a copy of this book.");
        }

        var checkOut = book.CheckOut();
        if (!checkOut.IsSuccess)
        {
            return checkOut.Error!;
        }

        return Loan.Start(book.Id, member.Id, now, LoanPeriod);
    }

    /// <summary>Every started day after the due date costs <see cref="DailyLateFee"/>.</summary>
    public static decimal LateFeeFor(DateTimeOffset dueAt, DateTimeOffset returnedAt)
    {
        if (returnedAt <= dueAt)
        {
            return 0m;
        }

        var daysLate = (int)Math.Ceiling((returnedAt - dueAt).TotalDays);
        return daysLate * DailyLateFee;
    }
}
