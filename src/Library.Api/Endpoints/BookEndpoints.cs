using Library.Api.Infrastructure;
using Library.Application.Abstractions;
using Library.Application.Books;

namespace Library.Api.Endpoints;

public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/books").WithTags("Books");

        books.MapGet("/", async ([AsParameters] BookSearch search, IBookQueries queries, CancellationToken ct) =>
                Results.Ok(await queries.SearchAsync(search, ct)))
            .WithName("SearchBooks")
            .WithSummary("Searches the catalogue by title, author or ISBN")
            .WithValidation<BookSearch>()
            .Produces<PagedResult<BookResponse>>();

        books.MapGet("/{id:guid}", async (Guid id, IBookQueries queries, CancellationToken ct) =>
                await queries.GetAsync(id, ct) is { } book ? Results.Ok(book) : Results.NotFound())
            .WithName("GetBook")
            .Produces<BookResponse>()
            .Produces(StatusCodes.Status404NotFound);

        books.MapPost("/", async (AddBookCommand command, AddBook useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(command, ct);
                return result.IsSuccess
                    ? Results.Created($"/api/books/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Policies.Librarian)
            .WithName("AddBook")
            .WithValidation<AddBookCommand>()
            .Produces<BookResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        books.MapPost("/{id:guid}/copies", async (Guid id, AddCopiesCommand command, AddCopies useCase, CancellationToken ct) =>
            {
                var result = await useCase.HandleAsync(id, command, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
            })
            .RequireAuthorization(Policies.Librarian)
            .WithName("AddCopies")
            .WithValidation<AddCopiesCommand>()
            .Produces<BookResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
