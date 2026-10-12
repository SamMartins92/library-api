using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Members;

namespace Library.Domain.Tests;

public sealed class LoanPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly Member _member = Member.Register("Ana", "ana@example.com", "hash", Role.Member, Now);

    private static Book NewBook(int copies = 2) =>
        Book.Create(Isbn.Create("9780132350884").Value, "Clean Code", "Robert C. Martin", 2008, copies);

    private Loan BorrowOk(Book book, DateTimeOffset at, params Loan[] existing) =>
        LoanPolicy.Borrow(_member, book, existing, at).Value;

    [Fact]
    public void Borrow_StartsLoanForFourteenDays_AndTakesACopy()
    {
        // Arrange
        var book = NewBook(copies: 2);

        // Act
        var loan = LoanPolicy.Borrow(_member, book, [], Now);

        // Assert
        Assert.True(loan.IsSuccess);
        Assert.Equal(Now.AddDays(14), loan.Value.DueAt);
        Assert.Equal(1, book.AvailableCopies);
    }

    [Fact]
    public void Borrow_WhenNoCopyIsAvailable_IsAConflict()
    {
        // Arrange
        var book = NewBook(copies: 1);
        BorrowOk(book, Now);

        // Act
        var second = LoanPolicy.Borrow(_member, book, [], Now);

        // Assert
        Assert.Equal("Book.Unavailable", second.Error!.Code);
        Assert.Equal(0, book.AvailableCopies);
    }

    [Fact]
    public void Borrow_BeyondTheLimit_IsAConflict()
    {
        // Arrange
        var existing = Enumerable.Range(0, LoanPolicy.MaxActiveLoans).Select(_ => BorrowOk(NewBook(), Now)).ToArray();
        var book = NewBook();

        // Act
        var result = LoanPolicy.Borrow(_member, book, existing, Now);

        // Assert
        Assert.Equal("Loan.Limit", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(2, book.AvailableCopies);
    }

    [Fact]
    public void Borrow_WithAnOverdueLoan_IsForbidden()
    {
        // Arrange
        var overdue = BorrowOk(NewBook(), Now.AddDays(-20));

        // Act
        var result = LoanPolicy.Borrow(_member, NewBook(), [overdue], Now);

        // Assert
        Assert.Equal("Loan.Overdue", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public void Borrow_SameBookTwice_IsAConflict()
    {
        // Arrange
        var book = NewBook(copies: 3);
        var first = BorrowOk(book, Now);

        // Act
        var result = LoanPolicy.Borrow(_member, book, [first], Now);

        // Assert
        Assert.Equal("Loan.Duplicate", result.Error!.Code);
    }

    [Theory]
    [InlineData(0, 0)]        // on the due date
    [InlineData(1, 0.50)]     // one hour late counts as a started day
    [InlineData(24, 0.50)]
    [InlineData(25, 1.00)]
    [InlineData(72, 1.50)]
    public void LateFee_IsChargedPerStartedDay(int hoursLate, double expectedFee)
    {
        // Arrange
        var due = Now;

        // Act
        var fee = LoanPolicy.LateFeeFor(due, due.AddHours(hoursLate));

        // Assert
        Assert.Equal((decimal)expectedFee, fee);
    }

    [Fact]
    public void Return_PutsTheCopyBack_AndRecordsTheFee()
    {
        // Arrange
        var book = NewBook(copies: 1);
        var loan = BorrowOk(book, Now);
        var returnedAt = loan.DueAt.AddDays(2);

        // Act
        var result = loan.Return(book, returnedAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, book.AvailableCopies);
        Assert.Equal(returnedAt, loan.ReturnedAt);
        Assert.Equal(1.00m, loan.LateFee);
        Assert.False(loan.IsActive);
    }

    [Fact]
    public void Return_Twice_IsAConflict()
    {
        // Arrange
        var book = NewBook();
        var loan = BorrowOk(book, Now);
        loan.Return(book, Now.AddDays(1));

        // Act
        var second = loan.Return(book, Now.AddDays(2));

        // Assert
        Assert.Equal("Loan.AlreadyReturned", second.Error!.Code);
        Assert.Equal(2, book.AvailableCopies);
    }
}
