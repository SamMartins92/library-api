using Library.Application.Abstractions;
using Library.Domain.Books;
using Library.Domain.Members;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies migrations and, when <c>Seed:Enabled</c> is true, creates the librarian account
    /// (credentials from configuration) and a handful of books.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("Seed:Enabled"))
        {
            return;
        }

        var email = configuration["Seed:LibrarianEmail"];
        var password = configuration["Seed:LibrarianPassword"];
        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password)
            && !await db.Members.AnyAsync(m => m.Email == Member.NormalizeEmail(email), cancellationToken))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            db.Members.Add(Member.Register("Librarian", email, hasher.Hash(password), Role.Librarian, clock.GetUtcNow()));
        }

        if (!await db.Books.AnyAsync(cancellationToken))
        {
            db.Books.AddRange(
                Book.Create(Isbn.Create("978-0-13-235088-4").Value, "Clean Code", "Robert C. Martin", 2008, 3),
                Book.Create(Isbn.Create("978-0-13-595705-9").Value, "The Pragmatic Programmer", "David Thomas, Andrew Hunt", 2019, 2),
                Book.Create(Isbn.Create("978-0-321-12521-7").Value, "Domain-Driven Design", "Eric Evans", 2003, 2),
                Book.Create(Isbn.Create("978-1-4493-7332-0").Value, "Designing Data-Intensive Applications", "Martin Kleppmann", 2017, 1),
                Book.Create(Isbn.Create("978-0-13-475759-9").Value, "Refactoring", "Martin Fowler", 2018, 2));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
