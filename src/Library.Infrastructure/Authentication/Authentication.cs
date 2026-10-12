using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Library.Application.Abstractions;
using Library.Domain.Members;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Library.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 key; at least 32 characters. Comes from user secrets / environment, never from source control.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int LifetimeMinutes { get; init; } = 60;

    public SymmetricSecurityKey SecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(Member member)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(settings.LifetimeMinutes);

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, member.Email),
                new Claim(JwtRegisteredClaimNames.Name, member.Name),
                new Claim("role", member.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(settings.SecurityKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}

/// <summary>Adapter over ASP.NET Core Identity's hasher (PBKDF2 with a per-password salt).</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<Member> Hasher = new();

    public string Hash(string password) => Hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
