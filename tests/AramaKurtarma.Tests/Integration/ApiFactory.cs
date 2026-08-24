using AramaKurtarma.DataAccess.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace AramaKurtarma.Tests.Integration;

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
        .WithDatabase("arama_kurtarma_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

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
            services.RemoveAll<DbContextOptions<AramaKurtarmaDbContext>>();

            services.AddDbContext<AramaKurtarmaDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
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
        var optionsBuilder = new DbContextOptionsBuilder<AramaKurtarmaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString());

        await using (var dbContext = new AramaKurtarmaDbContext(optionsBuilder.Options))
        {
            await dbContext.Database.MigrateAsync();
        }

        // Migration'lar hazir - artik uygulamayi guvenle baslatabiliriz (seed basarili olur).
        _ = Services;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }
}
