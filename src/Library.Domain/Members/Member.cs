namespace Library.Domain.Members;

public enum Role
{
    Member,
    Librarian,
}

public sealed class Member
{
    private Member()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Role Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static Member Register(string name, string email, string passwordHash, Role role, DateTimeOffset joinedAt) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name.Trim(),
        Email = NormalizeEmail(email),
        PasswordHash = passwordHash,
        Role = role,
        JoinedAt = joinedAt,
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
