using Library.Api.Infrastructure;
using Library.Application.Abstractions;
using Library.Application.Loans;

namespace Library.Api.Endpoints;

public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var loans = app.MapGroup("/api/loans").WithTags("Loans").RequireAuthorization();

        loans.MapPost("/", async (BorrowBookCommand command, BorrowBook useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/loans/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .WithName("BorrowBook")
            .WithSummary("Borrows a copy of a book for the signed-in member")
            .Produces<LoanResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        loans.MapPost("/{id:guid}/return", async (Guid id, ReturnBook useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(id, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
            })
            .WithName("ReturnBook")
            .WithSummary("Returns a borrowed book; a late fee is charged per started day after the due date")
            .Produces<LoanResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        loans.MapGet("/mine", async (ILoanQueries queries, ICurrentUser user, TimeProvider clock, CancellationToken ct) =>
                Results.Ok(await queries.ForMemberAsync(user.MemberId, clock.GetUtcNow(), ct)))
            .WithName("MyLoans")
            .Produces<IReadOnlyList<LoanResponse>>();

        return app;
    }
}
