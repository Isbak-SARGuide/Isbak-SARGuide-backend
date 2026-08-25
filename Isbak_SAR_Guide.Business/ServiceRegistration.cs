using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Business.Services.Concrete;
using Isbak_SAR_Guide.Business.Validation.Books;
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
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISyncService, SyncService>();

        return services;
    }
}
