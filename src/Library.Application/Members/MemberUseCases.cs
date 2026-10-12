using FluentValidation;
using Library.Application.Abstractions;
using Library.Domain.Common;
using Library.Domain.Members;

namespace Library.Application.Members;

public sealed record MemberResponse(Guid Id, string Name, string Email, string Role, DateTimeOffset JoinedAt)
{
    public static MemberResponse From(Member member) =>
        new(member.Id, member.Name, member.Email, member.Role.ToString(), member.JoinedAt);
}

public sealed record RegisterMemberCommand(string Name, string Email, string Password);

public sealed class RegisterMemberValidator : AbstractValidator<RegisterMemberCommand>
{
    public RegisterMemberValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(c => c.Password)
            .MinimumLength(10).WithMessage("The password must have at least 10 characters.")
            .Matches("[A-Za-z]").WithMessage("The password must contain a letter.")
            .Matches("[0-9]").WithMessage("The password must contain a digit.");
    }
}

/// <summary>Self-service registration always creates a regular member; librarians are provisioned separately.</summary>
public sealed class RegisterMember(
    IMemberRepository members,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<MemberResponse>> HandleAsync(RegisterMemberCommand command, CancellationToken cancellationToken)
    {
        if (await members.FindByEmailAsync(Member.NormalizeEmail(command.Email), cancellationToken) is not null)
        {
            return Error.Conflict("Member.DuplicateEmail", "An account with this email already exists.");
        }

        var member = Member.Register(command.Name, command.Email, passwordHasher.Hash(command.Password), Role.Member,
            clock.GetUtcNow());
        members.Add(member);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? MemberResponse.From(member) : saved.Error!;
    }
}

public sealed record LoginCommand(string Email, string Password);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}

public sealed class Login(IMemberRepository members, IPasswordHasher passwordHasher, ITokenService tokens)
{
    private static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "The email or password is incorrect.");

    public async Task<Result<AccessToken>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var member = await members.FindByEmailAsync(Member.NormalizeEmail(command.Email), cancellationToken);

        // Same error for unknown email and wrong password, so accounts cannot be enumerated.
        if (member is null || !passwordHasher.Verify(member.PasswordHash, command.Password))
        {
            return InvalidCredentials;
        }

        return tokens.Issue(member);
    }
}
