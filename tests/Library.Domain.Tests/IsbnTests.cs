using Library.Domain.Books;

namespace Library.Domain.Tests;

public sealed class IsbnTests
{
    [Theory]
    [InlineData("978-0-13-235088-4", "9780132350884")]
    [InlineData("978 1 4493 7332 0", "9781449373320")]
    [InlineData("9790000000001", "9790000000001")]
    public void ValidIsbn_IsNormalised(string input, string expected)
    {
        // Act
        var isbn = Isbn.Create(input);

        // Assert
        Assert.True(isbn.IsSuccess);
        Assert.Equal(expected, isbn.Value.Value);
    }

    [Theory]
    [InlineData("978-0-13-235088-5", "Isbn.CheckDigit")]
    [InlineData("978013235088", "Isbn.Format")]
    [InlineData("97801323508841", "Isbn.Format")]
    [InlineData("978013235088X", "Isbn.Format")]
    [InlineData("1230132350884", "Isbn.Prefix")]
    [InlineData(null, "Isbn.Format")]
    public void InvalidIsbn_ReturnsValidationError(string? input, string expectedCode)
    {
        // Act
        var isbn = Isbn.Create(input);

        // Assert
        Assert.False(isbn.IsSuccess);
        Assert.Equal(expectedCode, isbn.Error!.Code);
    }
}
