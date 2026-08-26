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

    // Gercek kitap icerigi (kentsel-arama-kurtarma-operasyonlari.pdf) - modul
    // modul isleniyor. Kaynak PDF'in Icindekiler sayfa numaralari guvenilir
    // DEGIL (TOC drift - orn. "Sinyaller ve Uyarilar" TOC'ta sayfa 5 diyor,
    // gercekte sayfa 7'de basliyor); her modulun siniri metin okunarak
    // dogrulandi, sayfa numarasina mekanik guvenilmedi.
    //
    // BSAFE (s.4-6): tek Content, resim yok (ilk gomulu resim s.10'da).
    // Senaryo bazli gruplar ("Yangin durumunda;", "Rehin alinirsaniz;" gibi)
    // Warning, genel kurallar Text olarak siniflandirildi.
    private static readonly SeedModule[] _moduleSeeds =
    [
        new(
            "BSAFE",
            "Sahada kisisel guvenlik icin temel kurallar ve senaryo bazli davranis rehberi.",
            [
                new(
                    "BSAFE Güvenlik Kuralları",
                    "Kişisel güvenlik ilkeleri ve özel durumlarda izlenecek davranış kuralları.",
                    [
                        new(ContentBlockType.Text,
                            "- Güvenliğinizden nihai olarak kendiniz sorumlusunuz.\n" +
                            "- Güvenlik önlemleri makam, mevki ve yetki gözetmeksizin herkes için geçerlidir.\n" +
                            "- Mayın veya patlayıcı olduğunu düşündüğünüz yerlerde hareketsiz kalın ve derhal yardım çağrısında bulunun.\n" +
                            "- Diğer insanların kültürlerinin farkına vararak ve kendi kültürünüzü bilerek farkındalık oluşturabilirsiniz.\n" +
                            "- Röportaj vermeye yetkiniz yoksa ve vereceğiniz bilgiler güvenlik riski oluşturacaksa sorulara yanıt vermekten kaçının.\n" +
                            "- Üst düzey yetkili de olsanız taciz ve şiddet kuralları, cezaları sizler için de geçerlidir.\n" +
                            "- Güvenlik riskini yönetmek için tehdidin olasılığını ve riskini en aza indirmek gerekmektedir."),
                        new(ContentBlockType.Warning,
                            "Yangın durumlarında;\n" +
                            "- Yangın söndürücü kullanmayı bilin.\n" +
                            "- Yangın söndürücü türlerini tanıyın.\n" +
                            "- Acil durum çıkışlarının boş olduğundan emin olun.\n" +
                            "- Yangın tatbikatları yapın ve nereyi aramanız gerektiğini bilin."),
                        new(ContentBlockType.Text,
                            "- Sosyal medya hesaplarınızdan nerede olduğunuza dair bilgiler paylaşmayın. " +
                            "Evinizin boş olduğu manasına gelir ve hırsızlar için davetiye çıkarır."),
                        new(ContentBlockType.Warning,
                            "Asansörde rahatsız edilirseniz;\n" +
                            "- Bulunduğunuz yeri terk edin, yapamıyorsanız acil durum düğmesine yakın olun.\n" +
                            "- Cep telefonunuzda acil durum numaralarının kayıtlı olması bir hafifletme önlemidir."),
                        new(ContentBlockType.Text,
                            "- Cinsel saldırı durumunda önceliğinizin hayatta kalmak olduğunu unutmayın."),
                        new(ContentBlockType.Warning,
                            "HIV/AIDS veya kan yoluyla bulaşan hastalıkları önlemek için;\n" +
                            "- Kan, meni ve vajinal sıvılardan kaçının.\n" +
                            "- Korunmasız cinsel ilişkiye girmeyin."),
                        new(ContentBlockType.Text,
                            "- Bulunduğunuz mahallede çok fazla güvenlik tedbirleri alınmışsa bu oranın tehlikeli bir yer olduğuna işarettir.\n" +
                            "- Aracınızla seyir halinde iken, konu önemli bile olsa mesajlaşmayın. Telefonunuzu kullanmayın."),
                        new(ContentBlockType.Warning,
                            "Rehin alınırsanız;\n" +
                            "- Ani hareketlerde bulunmayın. Sabırlı olun, iletişiminize önem verin ve onların dilini biliyorsanız o şekilde hitap edin."),
                        new(ContentBlockType.Text,
                            "- Otele döndüğünüzde oda kapınızın açık olduğunu fark ederseniz içeriye girmeyin ve yetkililerden yardım isteyip onlarla birlikte hareket edin.\n" +
                            "- Sağlığınıza dikkat etmemek, tatil yapmamak, önemli veya gizli belgeleri gözetimsiz bırakmak, iş arkadaşlarınızın özel bilgilerini ifşa etmek, " +
                            "stresi yönetememek ve gerektiğinde yardım istememek veya çok fazla risk almak sizi ve etrafınızdakileri tehlikeye düşürecektir."),
                        new(ContentBlockType.Warning,
                            "Havaalanı, tren veya otobüs terminalinde iseniz;\n" +
                            "- Bagajınıza ve kişisel eşyalarınıza dikkat edin.\n" +
                            "- Acil çıkışların nerede olduğunu bilin.\n" +
                            "- Değerli eşyalarınızı her zaman yanınızda bulundurun.\n" +
                            "- Uyanık ve sakin olun."),
                        new(ContentBlockType.Text,
                            "- Konvoy halinde araçla seyir halinde iken kazaları önlemek için araçlar arasında takip mesafesi bırakın.\n" +
                            "- Herhangi bir yerel yönetimden olduğunu söyleyen ve sizden gerek yaptığınız iş, gerek kimlik bilgileriniz gerekse " +
                            "bulunduğunuz yere dair bilgiler isteyenlere paylaşımda bulunmayın.\n" +
                            "- Tüm çabalarınıza rağmen aracınızı kaçırmak isteyenlere karşı direnmeyin, elleriniz görüş alanında olsun ve ani hareketler yapmayın."),
                        new(ContentBlockType.Text,
                            "Bakmakla yükümlü olduğumuz kimseleri korumak için:\n" +
                            "- Çocuksa; okul görevlilerinin, otobüs şoförlerinin ve diğer personellerin telefon numaralarını bilmeliyiz.\n" +
                            "- Acil bir durumda ne yapmaları gerektiğini önceden bilmeliyiz.\n" +
                            "- Senaryo ve tatbikatlarla durumu pekiştirmeliyiz.\n\n" +
                            "- Takip edilirseniz panik yapmadan kalabalığa karışın. Araçla iseniz ana caddeye girip yönünüzü değiştirin, " +
                            "en yakın ve güvenli bölgeye ilerleyin (karakol, hastane vb.).\n" +
                            "- Patlayıcı görürseniz kesinlikle müdahale etmeyin ve ilgili birimlere bildirin.\n" +
                            "- Kampa giderseniz; bölgenin yerel yöneticilerine orada olduğunuzu, kim olduğunuzu ve işinizle ilgili bilgiler verin.\n" +
                            "- Gerekirse polis veya güvenliği hazır bulundurun."),
                        new(ContentBlockType.Text,
                            "- Tehdit içeren mesaj veya arama alırsanız tüm söylenenleri dikkate alın ve güvenlik yetkililerine bildirin.\n" +
                            "- Cinsel taciz kültürel farklılıktan açığa çıkar. Buna maruz kalmamak için müstehcen şakalar yapmayın.\n" +
                            "- Kültürel farkındalığı göz önünde bulundurun.\n" +
                            "- Çit, köpek, aydınlatma, alarm sistemi, harekete duyarlı sensör ve güvenlik personeli bulunan yapıların bulunduğu yerler, " +
                            "oradaki yerli halkın güvenlik endişesinden ve zarar görebilirliği en aza indirgemek için aldığı ÖNLEME TEDBİRLERİ'dir. " +
                            "Bu gibi yerlerin olduğu mahalleler GÜVENLİ DEĞİLDİR."),
                        new(ContentBlockType.Text,
                            "- Yurt dışı seyahati öncesi ve sonrası mutlaka doktor kontrolünden geçin.\n" +
                            "- Polis kontrol noktasına gelmeden önce hızınızı yavaşlatın, gerekirse durun. Araç içi aydınlatmalarını açın. " +
                            "Ellerinizi dışardan görünecek şekilde konumlandırın. Sakin ve sabırlı olun. Sorulan sorulara kısa ve net cevaplar verin. " +
                            "Aracınızda arama yapılacaksa mutlaka başında durun. Rüşvet teklif etmeyin.\n" +
                            "- Aracınızın bakımını düzenli aralıklarla yaptırın.\n" +
                            "- Aracınız kaçırılacaksa direnmeyin. Söylenenleri uygulayın, ani hareketlerden kaçının. Araçtan inerken kontağı kapatmayın.\n" +
                            "- Tehlikeli bölgelerde iken mücevher veya pahalı saatler takmamak ÖNLEME TEDBİRİ'dir.\n" +
                            "- Sıtma, zika gibi sivrisinekten bulaşacak hastalıklara karşı uzun kollu giyinmek, kovucu kullanmak gibi önleme tedbirleri almalıyız."),
                    ]),
            ]),
    ];
}
