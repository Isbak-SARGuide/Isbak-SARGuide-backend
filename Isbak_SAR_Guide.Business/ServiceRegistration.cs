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

        // JwtOptions burada bagli: TokenService bu katmanda ve IOptions<JwtOptions>
        // istiyor. Eskiden sadece API'deki AddApiAuthentication() bagliyordu -
        // AddBusiness() o zaman tek basina cagrildiginda TokenService'in bagimliligi
        // sessizce cozulmuyordu (varsayilan/bos bir JwtOptions ile calisirdi).
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);
        _ = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("'Jwt' konfigurasyon bolumu eksik.");

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
        services.AddScoped<IUserService, UserService>();

        // ISyncCache tek instance'lik icerik icin process-genelinde bir tek
        // IMemoryCache paylasmali - Scoped olsaydi her HTTP istegi kendi bos
        // cache'ini gorurdu, cache hic isabet almazdi (12.2).
        services.AddMemoryCache();
        services.AddSingleton<ISyncCache, MemoryCacheSyncCache>();

        return services;
    }
}
