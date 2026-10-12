using Library.Api.Endpoints;
using Library.Api.Infrastructure;
using Library.Application;
using Library.Application.Abstractions;
using Library.Infrastructure;
using Library.Infrastructure.Authentication;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Configured through options so it sees the final configuration, and validated against the same
// TimeProvider that issues the tokens (the token lifetime then follows the application's clock).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>, TimeProvider>((options, jwtOptions, clock) =>
    {
        var jwt = jwtOptions.Value;
        var skew = TimeSpan.FromSeconds(30);

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SecurityKey(),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = "role",
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return (notBefore is null || notBefore <= now + skew) && (expires is null || expires > now - skew);
            },
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Librarian, policy => policy.RequireRole("Librarian"));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.AddHealthChecks().AddDbContextCheck<LibraryDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Library API"));
app.MapHealthChecks("/health");

app.MapAuthEndpoints();
app.MapBookEndpoints();
app.MapLoanEndpoints();

await app.Services.InitializeDatabaseAsync();

app.Run();

public static class Policies
{
    public const string Librarian = nameof(Librarian);
}

public partial class Program;
