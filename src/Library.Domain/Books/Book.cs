using Library.Domain.Common;

namespace Library.Domain.Books;

public sealed class Book
{
    private Book()
    {
    }

    public Guid Id { get; private set; }

    public Isbn Isbn { get; private set; } = null!;

    public string Title { get; private set; } = string.Empty;

    public string Author { get; private set; } = string.Empty;

    public int PublishedYear { get; private set; }

    public int TotalCopies { get; private set; }

    public int AvailableCopies { get; private set; }

    /// <summary>Concurrency token: two members borrowing the last copy at the same time cannot both succeed.</summary>
    public int Version { get; private set; }

    public static Book Create(Isbn isbn, string title, string author, int publishedYear, int copies) => new()
    {
        Id = Guid.CreateVersion7(),
        Isbn = isbn,
        Title = title.Trim(),
        Author = author.Trim(),
        PublishedYear = publishedYear,
        TotalCopies = copies,
        AvailableCopies = copies,
    };

    public Result AddCopies(int copies)
    {
        if (copies <= 0)
        {
            return Error.Validation("Book.Copies", "The number of copies must be greater than zero.");
        }

        TotalCopies += copies;
        AvailableCopies += copies;
        Version++;
        return Result.Success();
    }

    internal Result CheckOut()
    {
        if (AvailableCopies == 0)
        {
            return Error.Conflict("Book.Unavailable", $"No copies of \"{Title}\" are available right now.");
        }

        AvailableCopies--;
        Version++;
        return Result.Success();
    }

    internal void CheckIn()
    {
        if (AvailableCopies >= TotalCopies)
        {
            throw new InvalidOperationException($"All copies of book {Id} are already on the shelf.");
        }

        AvailableCopies++;
        Version++;
    }
}
