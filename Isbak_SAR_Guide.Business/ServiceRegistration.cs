using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Business.Services.Concrete;
using Isbak_SAR_Guide.Business.Validation.Books;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        // Bu assembly'deki tum AbstractValidator<T> siniflarini otomatik bulup
        // IValidator<T> olarak kaydeder (CreateBookDtoValidator, UpdateBookDtoValidator, ...).
        services.AddValidatorsFromAssemblyContaining<CreateBookDtoValidator>();

        var storageSection = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(storageSection);
        _ = storageSection.Get<StorageOptions>()
            ?? throw new InvalidOperationException("'Storage' konfigurasyon bolumu eksik.");

        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IPublishingService, PublishingService>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IContentBlockService, ContentBlockService>();
        services.AddScoped<IStorageService, LocalFileStorageService>();
        services.AddScoped<IMediaService, MediaService>();

        return services;
    }
}
