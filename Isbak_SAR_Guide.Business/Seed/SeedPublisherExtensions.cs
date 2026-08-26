using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DatabaseSeeder (DataAccess) yalnizca DRAFT veri kurar - publish Business
/// katmaninin isi oldugu icin DataAccess'ten cagrilamaz (tek yonlu bagimlilik
/// kurali: DataAccess, Business'i tanimaz). Bu yuzden "seed kitabini
/// yayinla" adimi ayri, Business'ta yasar; Program.cs'ten SeedDatabaseAsync'in
/// HEMEN ARDINDAN cagrilir.
/// </summary>
public static class SeedPublisherExtensions
{
    private const string _seedBookSlug = "kentsel-arama-kurtarma-el-kitabi";

    /// <summary>
    /// Development ortaminda, seed kitabi hic yayinlanmamissa bir kez publish
    /// eder - boylece projeyi klonlayan herkes (ozellikle mobil gelistirici)
    /// /sync uclarindan ILK ANDAN ITIBAREN gercek veri alir; elle admin login
    /// + POST /books/{id}/publish yapmaya gerek kalmaz. Idempotent: kitap
    /// zaten yayinlanmissa (GetLatestVersionAsync > 0) hicbir sey yapmaz.
    /// </summary>
    public static async Task PublishSeedBookAsync(this IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var seedBook = (await unitOfWork.Books.FindAllAsync())
            .SingleOrDefault(b => b.Slug == _seedBookSlug);

        if (seedBook is null)
        {
            return;
        }

        var currentVersion = await unitOfWork.Publications.GetLatestVersionAsync(seedBook.Id);

        if (currentVersion > 0)
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await userManager.GetUsersInRoleAsync(RoleNames.Admin)).Single();

        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        var result = await publishingService.PublishAsync(seedBook.Id, admin.Id);

        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seed kitabı publish edilemedi: {result.Error!.Message}");
        }
    }
}
