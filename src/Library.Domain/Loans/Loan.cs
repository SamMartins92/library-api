using Library.Domain.Books;
using Library.Domain.Common;

namespace Library.Domain.Loans;

public sealed class Loan
{
    private Loan()
    {
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateTimeOffset BorrowedAt { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    public decimal LateFee { get; private set; }

    public bool IsActive => ReturnedAt is null;

    public bool IsOverdue(DateTimeOffset now) => IsActive && now > DueAt;

    internal static Loan Start(Guid bookId, Guid memberId, DateTimeOffset now, TimeSpan period) => new()
    {
        Id = Guid.CreateVersion7(),
        BookId = bookId,
        MemberId = memberId,
        BorrowedAt = now,
        DueAt = now + period,
    };

    public Result Return(Book book, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return Error.Conflict("Loan.AlreadyReturned", "This loan was already returned.");
        }

        if (book.Id != BookId)
        {
            throw new ArgumentException("The book does not belong to this loan.", nameof(book));
        }

        ReturnedAt = now;
        LateFee = LoanPolicy.LateFeeFor(DueAt, now);
        book.CheckIn();
        return Result.Success();
    }
}
