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
        // Arrange
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık");
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
    public async Task PublishAsync_UnchangedContent_ProducesIdenticalContentChecksums()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("Sabit İçerik", "Diğer İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        // Act - arada hicbir degisiklik yok
        var result = await PublishAsync(bookId, adminId);

        // Assert - publish bir komuttur, hedef duruma yakinsama degil: v2 acilir.
        result.Value.Version.ShouldBe(2);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var rows = await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId)
            .ToListAsync();

        // Deterministik serilestirme kanitlanir (6.2 karari): content bazinda
        // v1 ve v2 bayt bayt ayni payload + ayni checksum uretir.
        var v1ByContent = rows.Where(r => r.Version == 1).ToDictionary(r => r.ContentId);
        var v2ByContent = rows.Where(r => r.Version == 2).ToDictionary(r => r.ContentId);

        v2ByContent.Keys.ShouldBe(v1ByContent.Keys, ignoreOrder: true);
        foreach (var (contentId, v2Row) in v2ByContent)
        {
            v2Row.PayloadJson.ShouldBe(v1ByContent[contentId].PayloadJson);
            v2Row.Checksum.ShouldBe(v1ByContent[contentId].Checksum);
        }

        // Yayin-seviyesi checksum'lar ise FARKLI olmali - bug degil, tasarim:
        // o checksum snapshot'in ozeti ve snapshot Version alanini iceriyor
        // (v1'de 1, v2'de 2). Idempotency sozu content seviyesinde gecerli;
        // yayin seviyesinde versiyon zaten degisen seyin ta kendisi.
        var publications = await dbContext.Set<BookPublication>()
            .Where(p => p.BookId == bookId)
            .ToListAsync();

        publications.Single(p => p.Version == 1).Checksum
            .ShouldNotBe(publications.Single(p => p.Version == 2).Checksum);
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

    // ---- Yardimcilar ----

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

        var module = new Module { Name = "Test Modülü", DisplayOrder = 1 };

        for (var i = 0; i < contentTitles.Length; i++)
        {
            module.Contents.Add(new Content
            {
                Title = contentTitles[i],
                Summary = $"{contentTitles[i]} özeti",
                DisplayOrder = i + 1,
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
