using Library.Application.Abstractions;
using Library.Application.Members;
using Library.Domain.Common;
using Library.Domain.Members;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Library.Application.Tests;

public sealed class MemberUseCaseTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly IMemberRepository _members = Substitute.For<IMemberRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();

    public MemberUseCaseTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(call => "hashed:" + call.Arg<string>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    [Fact]
    public async Task Register_StoresAHashedPassword_AndTheMemberRole()
    {
        // Arrange
        var useCase = new RegisterMember(_members, _hasher, _unitOfWork, _clock);

        // Act
        var result = await useCase.HandleAsync(new RegisterMemberCommand("Ana", " Ana@Example.com ", "Password123"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("ana@example.com", result.Value.Email);
        Assert.Equal("Member", result.Value.Role);
        _members.Received(1).Add(Arg.Is<Member>(m => m.PasswordHash == "hashed:Password123"));
    }

    [Fact]
    public async Task Register_WithAnExistingEmail_IsAConflict()
    {
        // Arrange
        _members.FindByEmailAsync("ana@example.com", Arg.Any<CancellationToken>())
            .Returns(Member.Register("Ana", "ana@example.com", "x", Role.Member, _clock.GetUtcNow()));
        var useCase = new RegisterMember(_members, _hasher, _unitOfWork, _clock);

        // Act
        var result = await useCase.HandleAsync(new RegisterMemberCommand("Ana", "ANA@example.com", "Password123"),
            CancellationToken.None);

        // Assert
        Assert.Equal("Member.DuplicateEmail", result.Error!.Code);
        _members.DidNotReceive().Add(Arg.Any<Member>());
    }

    [Fact]
    public async Task Login_WithWrongPassword_AndUnknownEmail_FailTheSameWay()
    {
        // Arrange
        var member = Member.Register("Ana", "ana@example.com", "hash", Role.Member, _clock.GetUtcNow());
        _members.FindByEmailAsync("ana@example.com", Arg.Any<CancellationToken>()).Returns(member);
        _hasher.Verify("hash", Arg.Any<string>()).Returns(false);
        var useCase = new Login(_members, _hasher, _tokens);

        // Act
        var wrongPassword = await useCase.HandleAsync(new LoginCommand("ana@example.com", "nope"), CancellationToken.None);
        var unknownEmail = await useCase.HandleAsync(new LoginCommand("bob@example.com", "nope"), CancellationToken.None);

        // Assert
        Assert.Equal(wrongPassword.Error, unknownEmail.Error);
        Assert.Equal(ErrorType.Unauthorized, wrongPassword.Error!.Type);
        _tokens.DidNotReceive().Issue(Arg.Any<Member>());
    }

    [Theory]
    [InlineData("short1", false)]
    [InlineData("onlyletters", false)]
    [InlineData("1234567890", false)]
    [InlineData("Password123", true)]
    public void PasswordRules(string password, bool valid)
    {
        // Act
        var result = new RegisterMemberValidator().Validate(new RegisterMemberCommand("Ana", "ana@example.com", password));

        // Assert
        Assert.Equal(valid, result.IsValid);
    }
}
