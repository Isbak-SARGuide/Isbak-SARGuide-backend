using System.Security.Cryptography;
using System.Text;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Publish motorunun semantigi: versiyonlama, immutability, tombstone,
/// checksum invariant'i, idempotency. Bilerek HTTP degil dogrudan
/// IPublishingService test edilir - bu senaryolarin hicbiri HTTP'nin bilgisi
/// degil; altta yine gercek Postgres (jsonb, unique index, transaction) var.
///
/// Scope disiplini: Arrange/Act/Assert ayri scope (ayri DbContext) kullanir -
/// ayni context'ten okumak change tracker'daki bellek-ici nesneyi okumaktir,
/// test DB'ye hic yazilmamis degeri "kaydedilmis" sanip gecebilir.
/// Her test kendi kitabini yaratir; paylasilan seed Book (id 1) publish edilmez.
/// </summary>
[Collection("Api")]
public class PublishingTests(ApiFactory factory)
{
    [Fact]
    public async Task PublishAsync_FirstPublish_CreatesVersionOne()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("İlk Bölüm", "İkinci Bölüm");
        var adminId = await GetAdminIdAsync();

        // Act
        var result = await PublishAsync(bookId, adminId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(1);
        result.Value.BookId.ShouldBe(bookId);
        result.Value.ContentCount.ShouldBe(2);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var publication = await dbContext.Set<BookPublication>()
            .Include(p => p.PublishedContents)
            .SingleAsync(p => p.BookId == bookId);

        publication.Version.ShouldBe(1);
        publication.PublishedContents.Count.ShouldBe(2);
        publication.PublishedContents.ShouldAllBe(pc => !pc.IsDeleted && pc.Version == 1);

        // Checksum invariant'i evrensel: Checksum == SHA256(PayloadJson), her
        // satirda. Bilerek SnapshotBuilder degil bagimsiz SHA-256 ile dogrulanir
        // (ayni yardimciyla dogrulamak totoloji olurdu). Tek yerde test yeter.
        foreach (var row in publication.PublishedContents)
        {
            row.Checksum.ShouldBe(Sha256Hex(row.PayloadJson));

            // Kanonik form = wire format: camelCase, 5.0 wire sozlesmesiyle
            // hizali - Faz 4'te payload'in deserialize edilmeden aynen (verbatim)
            // gecirilmesinin on sarti. Case.Sensitive sart: ShouldContain
            // varsayilani case-insensitive, "Title" ile de gecerdi.
            row.PayloadJson.ShouldContain("\"title\"", Case.Sensitive);
            row.PayloadJson.ShouldNotContain("\"Title\"", Case.Sensitive);
        }

        // Invariant'in ucuncu ornegi: yayin checksum'i, SnapshotJson kolonunun
        // AYNEN SHA-256'si (taze scope'tan okunan degerle - DB'nin sakladigi
        // baytlar dogrulanir, change tracker'daki degil).
        publication.Checksum.ShouldBe(Sha256Hex(publication.SnapshotJson));

        var book = await dbContext.Set<Book>().SingleAsync(b => b.Id == bookId);
        book.Version.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_DraftChangedAfterPublish_PublishedPayloadUnchanged()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        var (payloadBefore, checksumBefore) = await GetSingleRowAsync(bookId, version: 1);

        // Act - draft'i degistir, yeniden publish ETME
        await MutateFirstContentAsync(bookId, c => c.Title = "Değişmiş Başlık");

        // Assert - v1 satiri dondu: eski baslik hala payload'da, checksum ayni
        var (payloadAfter, checksumAfter) = await GetSingleRowAsync(bookId, version: 1);
        payloadAfter.ShouldBe(payloadBefore);
        payloadAfter.ShouldContain("Orijinal Başlık");
        payloadAfter.ShouldNotContain("Değişmiş Başlık");
        checksumAfter.ShouldBe(checksumBefore);
    }

    [Fact]
    public async Task PublishAsync_SecondPublishWithChanges_CreatesVersionTwoAndPreservesVersionOne()
    {
        // Arrange - iki content: biri degisecek, digeri sabit kalacak.
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık", "Sabit İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);
        await MutateFirstContentAsync(bookId, c => c.Title = "Yeni Başlık");

        // Act
        var result = await PublishAsync(bookId, adminId);

        // Assert
        result.Value.Version.ShouldBe(2);

        var (payloadV1, _) = await GetSingleRowAsync(bookId, version: 1);
        var (payloadV2, _) = await GetSingleRowAsync(bookId, version: 2);

        payloadV2.ShouldContain("Yeni Başlık");
        // Immutability iki yayin arasinda da gecerli: v1 el degmemis.
        payloadV1.ShouldContain("Orijinal Başlık");
        payloadV1.ShouldNotContain("Yeni Başlık");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        // Journal modeli: v2'de YALNIZ degisen content'in satiri var -
        // "Sabit İçerik" degismedi, yeniden yazilmadi.
        var v2Rows = await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId && pc.Version == 2)
            .ToListAsync();
        v2Rows.Count.ShouldBe(1);

        var book = await dbContext.Set<Book>().SingleAsync(b => b.Id == bookId);
        book.Version.ShouldBe(2);
    }

    [Fact]
    public async Task PublishAsync_ContentDeleted_WritesTombstoneOnce()
    {
        // Arrange - iki content: biri yasayacak, biri silinecek
        var bookId = await CreateBookWithContentsAsync("Kalan", "Silinecek");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        int deletedContentId;
        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
            var doomed = book!.Modules.Single().Contents.Single(c => c.Title == "Silinecek");
            deletedContentId = doomed.Id;

            // Soft delete: SaveChanges Deleted state'i IsDeleted=true'ya cevirir;
            // GetWithFullTreeAsync global filtre sayesinde onu artik gormez -
            // tombstone diff'i tam bu yuzden calisir.
            unitOfWork.Contents.Remove(doomed);
            await unitOfWork.SaveChangesAsync();
        }

        // Act - v2: tombstone yazilmali
        await PublishAsync(bookId, adminId);

        // Assert
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

            var tombstone = await dbContext.Set<PublishedContent>()
                .SingleAsync(pc => pc.BookId == bookId && pc.Version == 2 && pc.ContentId == deletedContentId);

            tombstone.IsDeleted.ShouldBeTrue();
            tombstone.PayloadJson.ShouldBe("{}");
            tombstone.Checksum.ShouldBe(Sha256Hex("{}"));

            // Sozlesme: silinen content SNAPSHOT'ta yoktur (tombstone yalnizca
            // PublishedContent kavramidir). Bugun bunu saglayan sey
            // GetWithFullTreeAsync'in soft-delete filtresi - yarin biri o
            // filtreyi kaldirirsa bu assert patlar, "tesadufen dogru" olmaz.
            var v2Publication = await dbContext.Set<BookPublication>()
                .SingleAsync(p => p.BookId == bookId && p.Version == 2);
            v2Publication.SnapshotJson.ShouldNotContain("Silinecek");

            // Journal modeli: v2'nin TEK satiri tombstone - "Kalan" degismedi,
            // yeniden yazilmadi.
            var v2RowCount = await dbContext.Set<PublishedContent>()
                .CountAsync(pc => pc.BookId == bookId && pc.Version == 2);
            v2RowCount.ShouldBe(1);
        }

        // Act 2 - v3: tombstone TEKRARLANMAMALI (bir-kez kurali)
        await PublishAsync(bookId, adminId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

            var v3RowsForDeleted = await dbContext.Set<PublishedContent>()
                .Where(pc => pc.BookId == bookId && pc.Version == 3 && pc.ContentId == deletedContentId)
                .ToListAsync();

            v3RowsForDeleted.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task PublishAsync_NoRealChangeSinceLastPublish_IsNoOpAndDoesNotBumpVersion()
    {
        // Kullanicinin bulgusu: Yayinla, icerikte GERCEK bir degisiklik olmasa
        // bile her tiklamada yeni bir versiyon/BookPublication uretiyordu.
        // Arrange
        var bookId = await CreateBookWithContentsAsync("Sabit İçerik", "Diğer İçerik");
        var adminId = await GetAdminIdAsync();
        var first = await PublishAsync(bookId, adminId);

        // Act - arada hicbir degisiklik yok
        var second = await PublishAsync(bookId, adminId);

        // Assert - no-op: ikinci cagri BIRINCI yayinin sonucunu aynen doner,
        // yeni bir versiyon/BookPublication/PublishedContent satiri uretmez.
        second.Value.PublicationId.ShouldBe(first.Value.PublicationId);
        second.Value.Version.ShouldBe(1);
        second.Value.Checksum.ShouldBe(first.Value.Checksum);
        // Saniyeye yuvarlanmis karsilastirma: second.Value.PublishedAt DB'den
        // geri okunuyor (GetLatestSummaryAsync), first.Value.PublishedAt ise
        // ilk cagrinin bellek-ici DateTime.UtcNow'u - Postgres timestamp
        // hassasiyeti .NET tick'inden dusuk oldugu icin tam esitlik yerine
        // saniye hassasiyetiyle kiyaslaniyor.
        second.Value.PublishedAt.ShouldBe(first.Value.PublishedAt, TimeSpan.FromSeconds(1));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        (await dbContext.Set<BookPublication>().CountAsync(p => p.BookId == bookId)).ShouldBe(1);
        (await dbContext.Set<PublishedContent>().CountAsync(pc => pc.BookId == bookId)).ShouldBe(2);

        var book = await dbContext.Set<Book>().SingleAsync(b => b.Id == bookId);
        book.Version.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_BookDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var adminId = await GetAdminIdAsync();

        // Act
        var result = await PublishAsync(bookId: 999_999, adminId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task PublishAsync_UnpublishedModule_ExcludedFromSnapshotEntirely()
    {
        // Arrange - iki modul: biri yayinda, digeri (icindeki content'iyle
        // birlikte) taslak.
        var bookId = await CreateBookWithMixedPublishStateAsync();
        var adminId = await GetAdminIdAsync();

        // Act
        var result = await PublishAsync(bookId, adminId);

        // Assert - sadece yayinlanmis modulun content'i sayildi.
        result.Value.ContentCount.ShouldBe(1);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var publication = await dbContext.Set<BookPublication>().SingleAsync(p => p.BookId == bookId);
        publication.SnapshotJson.ShouldContain("Yayindaki İçerik");
        publication.SnapshotJson.ShouldNotContain("Taslak Modül");
        publication.SnapshotJson.ShouldNotContain("Taslak İçerik");
    }

    [Fact]
    public async Task PublishAsync_UnpublishedContentUnderPublishedModule_ExcludedFromSnapshot()
    {
        // Arrange
        var bookId = await CreateBookWithMixedPublishStateAsync();
        var adminId = await GetAdminIdAsync();

        // Act
        await PublishAsync(bookId, adminId);

        // Assert - yayindaki modulun ALTINDA da taslak content disarida kaldi
        // (modul yayinda olmasi tek basina yeterli degil, content kendi
        // flag'iyle AYRICA filtrelenir).
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var publication = await dbContext.Set<BookPublication>().SingleAsync(p => p.BookId == bookId);
        publication.SnapshotJson.ShouldNotContain("Yayindaki Modülde Taslak İçerik");
    }

    [Fact]
    public async Task PublishAsync_ContentFlippedToUnpublished_TombstonesOnNextPublish()
    {
        // Arrange - v1: yayinda bir content. Sonra taslaga cevrilir.
        var bookId = await CreateBookWithContentsAsync("Geri Alınacak İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        int contentId;
        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
            var content = book!.Modules.Single().Contents.Single();
            contentId = content.Id;
            content.IsPublished = false;
            await unitOfWork.SaveChangesAsync();
        }

        // Act - v2: filtre disina dusen content tombstone'lanmali (mevcut
        // AppendTombstones mekanizmasi - ozel kod gerekmedi).
        var result = await PublishAsync(bookId, adminId);

        // Assert
        result.Value.ContentCount.ShouldBe(0);

        using var scope2 = factory.Services.CreateScope();
        var dbContext = scope2.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var tombstone = await dbContext.Set<PublishedContent>()
            .SingleAsync(pc => pc.BookId == bookId && pc.Version == 2 && pc.ContentId == contentId);
        tombstone.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_FirstPublish_SetsBookIsPublishedTrue()
    {
        // Arrange - Faz 13.3 bugfix: Book.IsPublished hicbir yerde set
        // edilmiyordu, hep varsayilan false kaliyordu (Create/UpdateBookDto'da
        // hic yok, salt-okunur bir alan).
        var bookId = await CreateBookWithContentsAsync("İçerik");
        var adminId = await GetAdminIdAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
            var bookBefore = await dbContext.Set<Book>().SingleAsync(b => b.Id == bookId);
            bookBefore.IsPublished.ShouldBeFalse();
        }

        // Act
        await PublishAsync(bookId, adminId);

        // Assert
        using var scope2 = factory.Services.CreateScope();
        var dbContext2 = scope2.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var bookAfter = await dbContext2.Set<Book>().SingleAsync(b => b.Id == bookId);
        bookAfter.IsPublished.ShouldBeTrue();
    }

    // ---- Yardimcilar ----

    /// <summary>
    /// İki modul: biri (ve tek content'i) yayinda, digeri (ve tek content'i)
    /// taslak. Ayrica yayindaki modulun ALTINA bir taslak content daha eklenir
    /// (modul-seviyesi ve content-seviyesi filtrenin BAGIMSIZ calistigini
    /// kanitlamak icin).
    /// </summary>
    private async Task<int> CreateBookWithMixedPublishStateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var publishedModule = new Module
        {
            Name = "Yayındaki Modül",
            DisplayOrder = 1,
            IsPublished = true,
            Contents =
            {
                new Content { Title = "Yayindaki İçerik", DisplayOrder = 1, IsPublished = true },
                new Content { Title = "Yayindaki Modülde Taslak İçerik", DisplayOrder = 2, IsPublished = false },
            },
        };

        var draftModule = new Module
        {
            Name = "Taslak Modül",
            DisplayOrder = 2,
            IsPublished = false,
            Contents =
            {
                new Content { Title = "Taslak İçerik", DisplayOrder = 1, IsPublished = true },
            },
        };

        var book = new Book
        {
            Title = "Karışık Yayın Durumu Kitabı",
            Slug = $"mixed-publish-test-{Guid.NewGuid():N}",
            Modules = { publishedModule, draftModule },
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }

    private async Task<Result<Business.DTOs.Publishing.PublishResultDto>> PublishAsync(int bookId, string adminId)
    {
        // Act her zaman kendi scope'unda kosar - publish'in gordugu context,
        // Arrange/Assert'in context'i degildir (gercek istek gibi).
        using var scope = factory.Services.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        return await publishingService.PublishAsync(bookId, adminId);
    }

    private async Task<int> CreateBookWithContentsAsync(params string[] contentTitles)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Faz 13.3: IsPublished=true burada BILINCLI - bu yardimci "publish
        // motorunun payload/tombstone/checksum davranisini test eden" mevcut
        // 18 testin ortak Arrange'i, IsPublished filtresinin KENDISINI test
        // etmiyorlar. O davranisin kendi testleri asagida ayri, kendi
        // draft/published Module-Content kombinasyonlarini acikca kuruyor.
        var module = new Module { Name = "Test Modülü", DisplayOrder = 1, IsPublished = true };

        for (var i = 0; i < contentTitles.Length; i++)
        {
            module.Contents.Add(new Content
            {
                Title = contentTitles[i],
                Summary = $"{contentTitles[i]} özeti",
                DisplayOrder = i + 1,
                IsPublished = true,
                Blocks =
                {
                    new ContentBlock { Type = ContentBlockType.Text, Text = $"{contentTitles[i]} metni", DisplayOrder = 1 },
                },
            });
        }

        var book = new Book
        {
            Title = "Publish Test Kitabı",
            Slug = $"publish-test-{Guid.NewGuid():N}",
            Modules = { module },
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }

    private async Task MutateFirstContentAsync(int bookId, Action<Content> mutate)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);

        var content = book!.Modules
            .SelectMany(m => m.Contents)
            .OrderBy(c => c.DisplayOrder)
            .First();

        mutate(content);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task<(string PayloadJson, string Checksum)> GetSingleRowAsync(int bookId, int version)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var row = await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId && pc.Version == version)
            .OrderBy(pc => pc.ContentId)
            .FirstAsync();

        return (row.PayloadJson, row.Checksum);
    }

    private async Task<string> GetAdminIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var admin = await dbContext.Users.FirstAsync(u => u.UserName == "admin");
        return admin.Id;
    }

    private static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
