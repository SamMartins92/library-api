using Library.Api.Infrastructure;
using Library.Application.Abstractions;
using Library.Application.Members;

namespace Library.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/register", async (RegisterMemberCommand command, RegisterMember useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/members/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .WithName("Register")
            .WithSummary("Creates a member account")
            .WithValidation<RegisterMemberCommand>()
            .Produces<MemberResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", async (LoginCommand command, Login useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(command, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithName("Login")
            .WithSummary("Exchanges email and password for a bearer token")
            .WithValidation<LoginCommand>()
            .Produces<AccessToken>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
