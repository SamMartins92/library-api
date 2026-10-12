using FluentValidation;
using Library.Application.Abstractions;
using Library.Domain.Books;
using Library.Domain.Common;

namespace Library.Application.Books;

public sealed record BookResponse(
    Guid Id, string Isbn, string Title, string Author, int PublishedYear, int TotalCopies, int AvailableCopies)
{
    public static BookResponse From(Book book) => new(
        book.Id, book.Isbn.Value, book.Title, book.Author, book.PublishedYear, book.TotalCopies, book.AvailableCopies);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record BookSearch(string? Query = null, bool AvailableOnly = false, int Page = 1, int PageSize = 20);

public sealed class BookSearchValidator : AbstractValidator<BookSearch>
{
    public BookSearchValidator()
    {
        RuleFor(s => s.Page).GreaterThanOrEqualTo(1);
        RuleFor(s => s.PageSize).InclusiveBetween(1, 100);
        RuleFor(s => s.Query).MaximumLength(100);
    }
}

public sealed record AddBookCommand(string Isbn, string Title, string Author, int PublishedYear, int Copies);

public sealed class AddBookValidator : AbstractValidator<AddBookCommand>
{
    public AddBookValidator(TimeProvider clock)
    {
        RuleFor(c => c.Isbn).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(300);
        RuleFor(c => c.Author).NotEmpty().MaximumLength(200);
        RuleFor(c => c.PublishedYear).InclusiveBetween(1450, clock.GetUtcNow().Year);
        RuleFor(c => c.Copies).InclusiveBetween(1, 1000);
    }
}

public sealed class AddBook(IBookRepository books, IUnitOfWork unitOfWork)
{
    public async Task<Result<BookResponse>> HandleAsync(AddBookCommand command, CancellationToken cancellationToken)
    {
        var isbn = Isbn.Create(command.Isbn);
        if (!isbn.IsSuccess)
        {
            return isbn.Error!;
        }

        if (await books.IsbnExistsAsync(isbn.Value, cancellationToken))
        {
            return Error.Conflict("Book.DuplicateIsbn", $"A book with ISBN {isbn.Value} already exists.");
        }

        var book = Book.Create(isbn.Value, command.Title, command.Author, command.PublishedYear, command.Copies);
        books.Add(book);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? BookResponse.From(book) : saved.Error!;
    }
}

public sealed record AddCopiesCommand(int Copies);

public sealed class AddCopiesValidator : AbstractValidator<AddCopiesCommand>
{
    public AddCopiesValidator() => RuleFor(c => c.Copies).InclusiveBetween(1, 1000);
}

public sealed class AddCopies(IBookRepository books, IUnitOfWork unitOfWork)
{
    public async Task<Result<BookResponse>> HandleAsync(Guid bookId, AddCopiesCommand command,
        CancellationToken cancellationToken)
    {
        var book = await books.GetAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Error.NotFound("Book.NotFound", "The book does not exist.");
        }

        var added = book.AddCopies(command.Copies);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? BookResponse.From(book) : saved.Error!;
    }
}
