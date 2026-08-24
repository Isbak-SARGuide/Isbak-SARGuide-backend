using AramaKurtarma.Business.Books;
using FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        // Bu assembly'deki tum AbstractValidator<T> siniflarini otomatik bulup
        // IValidator<T> olarak kaydeder (CreateBookDtoValidator, UpdateBookDtoValidator, ...).
        services.AddValidatorsFromAssemblyContaining<CreateBookDtoValidator>();

        services.AddScoped<IBookService, BookService>();

        return services;
    }
}
