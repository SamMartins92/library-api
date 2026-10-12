using Library.Domain.Common;

namespace Library.Domain.Books;

/// <summary>An ISBN-13, stored without separators and validated with its check digit.</summary>
public sealed record Isbn
{
    private Isbn(string value) => Value = value;

    public string Value { get; }

    public static Result<Isbn> Create(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(c => c is not ('-' or ' ')).ToArray());

        if (digits.Length != 13 || !digits.All(char.IsAsciiDigit))
        {
            return Error.Validation("Isbn.Format", "An ISBN must have 13 digits (hyphens and spaces are ignored).");
        }

        if (!digits.StartsWith("978", StringComparison.Ordinal) && !digits.StartsWith("979", StringComparison.Ordinal))
        {
            return Error.Validation("Isbn.Prefix", "An ISBN-13 starts with 978 or 979.");
        }

        var sum = digits[..12].Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        var checkDigit = (10 - sum % 10) % 10;
        if (digits[12] - '0' != checkDigit)
        {
            return Error.Validation("Isbn.CheckDigit", "The ISBN check digit is not valid.");
        }

        return new Isbn(digits);
    }

    /// <summary>Rehydrates a value that was validated before being stored.</summary>
    public static Isbn FromTrusted(string value) => new(value);

    public override string ToString() => Value;
}
