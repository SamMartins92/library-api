using Library.Application.Abstractions;
using Library.Application.Loans;
using Library.Domain.Books;
using Library.Domain.Common;
using Library.Domain.Loans;
using Library.Domain.Members;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Library.Application.Tests;

public sealed class BorrowBookTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly IBookRepository _books = Substitute.For<IBookRepository>();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly ILoanRepository _loans = Substitute.For<ILoanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private readonly Member _member;
    private readonly Book _book = Book.Create(Isbn.Create("9780132350884").Value, "Clean Code", "Robert C. Martin", 2008, 1);

    public BorrowBookTests()
    {
        _member = Member.Register("Ana", "ana@example.com", "hash", Role.Member, _clock.GetUtcNow());
        _currentUser.MemberId.Returns(_member.Id);
        _members.GetAsync(_member.Id, Arg.Any<CancellationToken>()).Returns(_member);
        _books.GetAsync(_book.Id, Arg.Any<CancellationToken>()).Returns(_book);
        _loans.GetActiveForMemberAsync(_member.Id, Arg.Any<CancellationToken>()).Returns([]);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private BorrowBook CreateUseCase() => new(_books, _members, _loans, _unitOfWork, _currentUser, _clock);

    [Fact]
    public async Task Success_AddsTheLoan_AndSaves()
    {
        // Act
        var result = await CreateUseCase().HandleAsync(new BorrowBookCommand(_book.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Clean Code", result.Value.BookTitle);
        Assert.Equal(_clock.GetUtcNow().AddDays(14), result.Value.DueAt);
        _loans.Received(1).Add(Arg.Is<Loan>(l => l.BookId == _book.Id && l.MemberId == _member.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownBook_IsNotFound()
    {
        // Act
        var result = await CreateUseCase().HandleAsync(new BorrowBookCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PolicyViolation_IsReturned_AndNothingIsSaved()
    {
        // Arrange
        var otherBooks = Enumerable.Range(0, LoanPolicy.MaxActiveLoans)
            .Select(_ => Book.Create(Isbn.Create("9781449373320").Value, "Book", "Author", 2017, 1))
            .Select(b => LoanPolicy.Borrow(_member, b, [], _clock.GetUtcNow()).Value)
            .ToList();
        _loans.GetActiveForMemberAsync(_member.Id, Arg.Any<CancellationToken>()).Returns(otherBooks);

        // Act
        var result = await CreateUseCase().HandleAsync(new BorrowBookCommand(_book.Id), CancellationToken.None);

        // Assert
        Assert.Equal("Loan.Limit", result.Error!.Code);
        _loans.DidNotReceive().Add(Arg.Any<Loan>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConcurrentCheckout_SurfacesTheConflict()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("Concurrency", "changed")));

        // Act
        var result = await CreateUseCase().HandleAsync(new BorrowBookCommand(_book.Id), CancellationToken.None);

        // Assert
        Assert.Equal("Concurrency", result.Error!.Code);
    }
}
