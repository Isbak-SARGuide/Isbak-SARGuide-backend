using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.DataAccess.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Testler icin gercek bir Postgres container'i ayaga kaldirir (Testcontainers)
/// ve API'yi bellekte (in-memory) calistirir. Tum test siniflari AYNI factory'yi
/// paylasir (bkz. ApiCollection) - her sinif icin ayri container acmak yavas olurdu.
///
/// BILINCLI SINIRLAMA: veritabani testler arasinda SIFIRLANMAZ, tum test calismasi
/// boyunca tek bir container/seed kullanilir. Bu yuzden testler kendi olusturduklari
/// veriyle calismali (orn. POST ile yeni kitap acmali), seed'deki ortak veriyi
/// (Book id=1, admin kullanicisi) SADECE okuma icin kullanmali, degistirmemeli.
/// Tam izolasyon (her test icin ayri transaction/rollback) ileride gerekirse eklenir.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("isbak_sar_guide_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    // Faz 6 (Media): testler gercek dosya yazar (LocalFileStorageService) -
    // repo'nun gercek storage/ klasorunu kirletmemek icin izole bir temp
    // klasore yonlendirilir, DisposeAsync'te silinir.
    private readonly string _storageTempPath = Path.Combine(Path.GetTempPath(), $"isbak-sar-guide-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Seed (DatabaseSeeder) sadece Development ortaminda calisiyor (Program.cs) -
        // testlerde de seed verisiyle calismak istedigimiz icin ortami sabitliyoruz.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Program.cs'teki AddDataAccess() gercek appsettings.Development.json'daki
            // (yerel Postgres'e isaret eden) baglantiyi kaydetmisti. Burada o kaydi
            // sokup, yerine Testcontainers'in urettigi GECICI baglanti dizesini koyuyoruz.
            services.RemoveAll<DbContextOptions<Isbak_SAR_GuideDbContext>>();

            services.AddDbContext<Isbak_SAR_GuideDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Program.cs'teki PostConfigure'dan SONRA calisir (kayit sirasi) -
            // gercek ContentRootPath'e cozulmus degeri temp klasorle ezer.
            services.PostConfigure<StorageOptions>(options => options.BasePath = _storageTempPath);

            // Faz 9.3: gercek limit (appsettings: 5/dk) paylasilan TestServer'da
            // TUM testlerin login/refresh cagrilarini AYNI partition'a toplar
            // (RemoteIpAddress in-memory sunucuda null'a duser) - gercek deger
            // kalsaydi bu dosyanin disindaki testler bile 429'a duserdi. 429
            // uretiminin kendisi bilerek otomatik testte degil, manuel curl ile
            // dogrulandi (roadmap'teki diger fazlarla ayni disiplin) - ayri bir
            // Postgres+host ayaga kaldirmak framework kodunu test etmek icin
            // orantisiz olurdu.
            services.PostConfigure<LoginRateLimitOptions>(options => options.PermitLimit = 100_000);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // ONEMLI: migration'lari, WebApplicationFactory'nin "Services" property'sine
        // HIC DOKUNMADAN, bagimsiz bir DbContext ile uyguluyoruz. Sebep: "Services"e
        // ilk erisim uygulamayi GERCEKTEN baslatir - bu da Program.cs'teki
        // SeedDatabaseAsync() cagrisini tetikler. Migration'lar henuz uygulanmamisken
        // seed calisirsa, "AspNetRoles tablosu yok" hatasiyla patlar. Once (bagimsiz
        // baglantiyla) migrate et, SONRA uygulamayi baslat.
        var optionsBuilder = new DbContextOptionsBuilder<Isbak_SAR_GuideDbContext>()
            .UseNpgsql(_postgres.GetConnectionString());

        await using (var dbContext = new Isbak_SAR_GuideDbContext(optionsBuilder.Options))
        {
            await dbContext.Database.MigrateAsync();
        }

        // Migration'lar hazir - artik uygulamayi guvenle baslatabiliriz (seed basarili olur).
        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();

        if (Directory.Exists(_storageTempPath))
        {
            Directory.Delete(_storageTempPath, recursive: true);
        }

        await base.DisposeAsync();
    }
}
