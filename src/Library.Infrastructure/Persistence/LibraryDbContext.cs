using Library.Domain.Books;
using Library.Domain.Loans;
using Library.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(book =>
        {
            book.ToTable("Books");
            book.HasKey(b => b.Id);
            book.Property(b => b.Isbn)
                .HasConversion(isbn => isbn.Value, value => Isbn.FromTrusted(value))
                .HasMaxLength(13)
                .IsRequired();
            book.HasIndex(b => b.Isbn).IsUnique();
            book.Property(b => b.Title).HasMaxLength(300).IsRequired();
            book.Property(b => b.Author).HasMaxLength(200).IsRequired();
            book.Property(b => b.Version).IsConcurrencyToken();
            book.ToTable(t => t.HasCheckConstraint("CK_Books_Copies", "AvailableCopies >= 0 AND AvailableCopies <= TotalCopies"));
        });

        modelBuilder.Entity<Member>(member =>
        {
            member.ToTable("Members");
            member.HasKey(m => m.Id);
            member.Property(m => m.Name).HasMaxLength(100).IsRequired();
            member.Property(m => m.Email).HasMaxLength(254).IsRequired();
            member.HasIndex(m => m.Email).IsUnique();
            member.Property(m => m.PasswordHash).IsRequired();
            member.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Loan>(loan =>
        {
            loan.ToTable("Loans");
            loan.HasKey(l => l.Id);
            loan.HasIndex(l => new { l.MemberId, l.ReturnedAt });
            loan.Property(l => l.LateFee).HasPrecision(10, 2);
            loan.Ignore(l => l.IsActive);
            loan.HasOne<Book>().WithMany().HasForeignKey(l => l.BookId).OnDelete(DeleteBehavior.Restrict);
            loan.HasOne<Member>().WithMany().HasForeignKey(l => l.MemberId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
