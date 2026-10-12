using FluentValidation;
using Library.Application.Books;
using Library.Application.Loans;
using Library.Application.Members;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterMember>();
        services.AddScoped<Login>();
        services.AddScoped<AddBook>();
        services.AddScoped<AddCopies>();
        services.AddScoped<BorrowBook>();
        services.AddScoped<ReturnBook>();

        services.AddValidatorsFromAssemblyContaining<AddBookValidator>(includeInternalTypes: true);
        return services;
    }
}
