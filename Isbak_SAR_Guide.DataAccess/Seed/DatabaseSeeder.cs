using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Isbak_SAR_Guide.DataAccess.Seed;

/// <summary>
/// Sadece Development ortaminda calisir (bkz. Program.cs). Idempotent'tir:
/// roller/admin/kitap zaten varsa hicbir sey yapmaz, tekrar tekrar
/// calistirmak guvenlidir.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager, configuration);
        await SeedContentTreeAsync(dbContext);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = [RoleNames.Admin, RoleNames.Editor];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        // Sadece yerel gelistirme icin varsayilan deger. appsettings.Development.local.json
        // (gitignore'da) ile ezilebilir. Production'da bu seed hic calismaz (Program.cs).
        var userName = configuration["Seed:AdminUserName"] ?? "admin";
        var password = configuration["Seed:AdminPassword"] ?? "Admin!Dev123";

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@isbak-sar-guide.local",
            EmailConfirmed = true,
            FullName = "Sistem Yoneticisi",
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Admin kullanicisi olusturulamadi: {errors}");
        }

        await userManager.AddToRoleAsync(admin, RoleNames.Admin);
    }

    private static async Task SeedContentTreeAsync(Isbak_SAR_GuideDbContext dbContext)
    {
        if (await dbContext.Books.AnyAsync())
        {
            return;
        }

        var book = new Book
        {
            Title = "Kentsel Arama Kurtarma El Kitabı",
            Slug = "kentsel-arama-kurtarma-el-kitabi",
            Description = "Kentsel arama kurtarma operasyonlarında görev alan ekipler için temel başvuru kaynağı.",
            LanguageCode = "tr",
            Version = 0,
            IsPublished = false,
        };

        var moduleOrder = 0;
        foreach (var moduleSeed in _moduleSeeds)
        {
            var module = new Module
            {
                Name = moduleSeed.Name,
                Description = moduleSeed.Description,
                DisplayOrder = moduleOrder++,
                IsPublished = false,
            };

            var contentOrder = 0;
            foreach (var contentSeed in moduleSeed.Contents)
            {
                var content = new Content
                {
                    Title = contentSeed.Title,
                    Summary = contentSeed.Summary,
                    DisplayOrder = contentOrder++,
                    IsPublished = false,
                };

                var blockOrder = 0;
                foreach (var blockSeed in contentSeed.Blocks)
                {
                    content.Blocks.Add(new ContentBlock
                    {
                        Type = blockSeed.Type,
                        Text = blockSeed.Text,
                        DataJson = blockSeed.DataJson,
                        DisplayOrder = blockOrder++,
                    });
                }

                module.Contents.Add(content);
            }

            book.Modules.Add(module);
        }

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync();
    }

    private sealed record SeedBlock(ContentBlockType Type, string? Text, string? DataJson = null);

    private sealed record SeedContent(string Title, string Summary, SeedBlock[] Blocks);

    private sealed record SeedModule(string Name, string Description, SeedContent[] Contents);

    // NOT: Bu icerikler gercek USAR (Urban Search & Rescue) terminolojisine
    // dayanir - admin panel demo'sunda ve sync testlerinde anlamli veri
    // gormek icin "Content 1, Content 2..." yerine gercek basliklar kullanildi.
    // Tam kapsamli icerik yazimi ilerleyen fazlarda admin panel uzerinden yapilacak.
    private static readonly SeedModule[] _moduleSeeds =
    [
        new(
            "Enkaz Altında Arama Teknikleri",
            "Çökme sonrası kayıp kişilerin tespiti için kullanılan sistematik arama yöntemleri.",
            [
                new(
                    "Sesli ve Görsel Arama Yöntemi",
                    "Elektronik ekipman olmadan uygulanabilen temel arama tekniği.",
                    [
                        new(ContentBlockType.Text,
                            "Sesli arama, enkaz alanında belirli aralıklarla tam sessizlik sağlanarak " +
                            "kayıp kişilerden gelebilecek ses veya vurma sinyallerinin dinlenmesi esasına dayanır."),
                        new(ContentBlockType.Warning,
                            "Sesli arama sirasinda tum ekipman ve jeneratorler durdurulmalidir.",
                            """{"severity":"high"}"""),
                    ]),
                new(
                    "Arama Köpekleri ile Koordinasyon",
                    "Köpek ekipleriyle çalışırken ekip güvenliği ve alan yönetimi.",
                    [
                        new(ContentBlockType.Text,
                            "Arama köpeği enkaz üzerinde çalışırken alanda gereksiz personel " +
                            "bulundurulmamalı, köpek eğitmeninin verdiği işaretler tüm ekiple paylaşılmalıdır."),
                    ]),
                new(
                    "Elektronik Arama Cihazlarının Kullanımı",
                    "Akustik ve optik arama cihazlarının saha kullanımı.",
                    [
                        new(ContentBlockType.Text,
                            "Akustik dinleme cihazları enkaz içindeki minimal titreşimleri algılayarak " +
                            "kayıp kişinin yaklaşık konumunu belirlemede kullanılır."),
                    ]),
                new(
                    "Enkaz Katmanlarının Sınıflandırılması",
                    "Farklı çökme tiplerinde oluşan boşluk ve katman yapıları.",
                    [
                        new(ContentBlockType.Table,
                            null,
                            """
                            {"headers":["Çökme Tipi","Tipik Boşluk"],
                             "rows":[["V-Şekli","Büyük, erişilebilir"],
                                     ["Kayma Tipi","Dar, dikkatli giriş gerekir"],
                                     ["Tam Çökme","Boşluk az, yüksek risk"]]}
                            """),
                    ]),
            ]),
        new(
            "Bina Stabilite Değerlendirmesi",
            "Müdahale öncesi yapının güvenlik açısından hızlı değerlendirilmesi.",
            [
                new(
                    "Hızlı Yapısal Değerlendirme (Rapid Triage)",
                    "Girişten önce yapılması gereken ilk gözlem adımları.",
                    [
                        new(ContentBlockType.Text,
                            "Yapı dışarıdan gözlemlenerek çatlak yönü, eğim ve malzeme dökülmesi " +
                            "gibi belirtiler değerlendirilir; bu değerlendirme giriş kararını belirler."),
                    ]),
                new(
                    "Çökme Türleri ve Boşluk Analizi",
                    "Yapısal çökme paternlerinin tanımlanması.",
                    [
                        new(ContentBlockType.Text,
                            "Çökme türünün doğru tanımlanması, olası boşluk bölgelerinin ve " +
                            "kayıp kişilerin bulunma ihtimalinin yüksek olduğu alanların öngörülmesini sağlar."),
                    ]),
                new(
                    "Destekleme ve Payandalama Temelleri",
                    "Güvenli çalışma alanı oluşturmak için temel destekleme yöntemleri.",
                    [
                        new(ContentBlockType.Text,
                            "Destekleme, ekip enkaza girmeden önce kritik taşıyıcı elemanların " +
                            "geçici olarak sabitlenmesi işlemidir."),
                    ]),
                new(
                    "Giriş Öncesi Güvenlik Kontrol Listesi",
                    "Enkaza girmeden önce doğrulanması gereken maddeler.",
                    [
                        new(ContentBlockType.Warning,
                            "Ekip lideri onayi olmadan yuksek riskli alanlara giris yapilmaz.",
                            """{"severity":"critical"}"""),
                    ]),
            ]),
        new(
            "İlk Yardım ve Triyaj",
            "Çoklu kayıp/yaralı durumlarında öncelik belirleme ve temel müdahale.",
            [
                new(
                    "START Triyaj Yöntemi",
                    "Hızlı sınıflandırma için kullanılan dört kategorili triyaj sistemi.",
                    [
                        new(ContentBlockType.Table,
                            null,
                            """
                            {"headers":["Kategori","Renk","Aciklama"],
                             "rows":[["Acil","Kırmızı","Hayati mudahale gerekli"],
                                     ["Bekleyebilir","Sarı","Stabil ama izlem gerekli"],
                                     ["Hafif","Yeşil","Kendi kendine hareket edebilir"],
                                     ["Exitus","Siyah","Mudahale onceligi yok"]]}
                            """),
                    ]),
                new(
                    "Crush Sendromu Belirtileri ve Müdahale",
                    "Uzun süreli sıkışmaya bağlı sistemik komplikasyonlar.",
                    [
                        new(ContentBlockType.Text,
                            "Crush sendromu, uzun sure enkaz altında sikisan doku hucrelerinin " +
                            "kurtarma sonrasi kan dolasimina zararli maddeler salmasi sonucu olusur."),
                    ]),
                new(
                    "Hipotermi ve Sıcak Çarpması Yönetimi",
                    "Saha koşullarında vücut ısısı dengesizliklerine müdahale.",
                    [
                        new(ContentBlockType.Text,
                            "Uzun sureli disari maruziyet, hem sicak hem soguk iklim kosullarinda " +
                            "kurtarilan kisilerde ciddi risk olusturur."),
                    ]),
                new(
                    "Temel Yaşam Desteği Uygulamaları",
                    "Kurtarma sonrası ilk müdahale sırasında temel yaşam desteği adımları.",
                    [
                        new(ContentBlockType.Text,
                            "Solunum ve dolasimin degerlendirilmesi, kurtarilan kisiye yapilacak " +
                            "ilk mudahalenin en oncelikli adimidir."),
                    ]),
            ]),
        new(
            "Ekip İçi İletişim ve Koordinasyon",
            "Saha operasyonlarında ekipler arası bilgi akışı ve komuta zinciri.",
            [
                new(
                    "Telsiz Haberleşme Protokolü",
                    "Standart telsiz terminolojisi ve kanal disiplini.",
                    [
                        new(ContentBlockType.Text,
                            "Telsiz haberlesmesinde net, kisa ve standart terminoloji kullanilmasi " +
                            "yanlis anlasilmalarin onune gecer."),
                    ]),
                new(
                    "El İşaretleri ve Sessiz Komutlar",
                    "Sesli arama sırasında kullanılan görsel komut sistemi.",
                    [
                        new(ContentBlockType.Text,
                            "Sesli arama fazlarinda telsiz kullanimi kisitlandigindan, ekip " +
                            "icinde standart el isaretleri kullanilir."),
                    ]),
                new(
                    "Görev Dağılımı ve Sektör Sorumluluğu",
                    "Geniş alanlarda sektör bazlı sorumluluk paylaşımı.",
                    [
                        new(ContentBlockType.Text,
                            "Saha, yonetilebilir sektorlere bolunerek her sektore bir ekip " +
                            "lideri ve sorumluluk alani atanir."),
                    ]),
                new(
                    "Vardiya Devir Teslim Prosedürü",
                    "Uzun süreli operasyonlarda ekip değişimi sırasında bilgi aktarımı.",
                    [
                        new(ContentBlockType.Text,
                            "Vardiya degisiminde alandaki durum, tespit edilen riskler ve " +
                            "devam eden islemler yeni ekibe eksiksiz aktarilmalidir."),
                    ]),
            ]),
    ];
}
