using System.Net;
using System.Text.Json;
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
/// 7.3-c delta dogruluk matrisi (7.5 spesifikasyonundaki senaryolar). Servis
/// dogrudan cagrilir (HTTP degil) - bu senaryolarin hicbiri HTTP'nin bilgisi
/// degil, motor semantigi burada kanitlanir (6.5'teki ayni karar). Delta
/// govdesi HAM JSON oldugu icin assertler JsonDocument.Parse ile okur -
/// salt-okur; verbatim kurali servis etmeyi kisitlar, okumayi degil.
/// </summary>
[Collection("Api")]
public class SyncChangesTests(ApiFactory factory)
{
    [Fact]
    public async Task GetChanges_ClientUpToDate_ReturnsEmptyChangesWithMatchingVersions()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId);

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        changes.RootElement.GetProperty("fromVersion").GetInt32().ShouldBe(1);
        changes.RootElement.GetProperty("toVersion").GetInt32().ShouldBe(1);
        // Faz 13.2: Modules gibi kosulsuz dolu - degisip degismedigine bakilmaksizin
        // ToVersion'daki guncel Book durumu her yanitta gelir.
        changes.RootElement.GetProperty("book").GetProperty("title").GetString().ShouldBe("Delta Test Kitabı");
        changes.RootElement.GetProperty("upsertedContents").GetArrayLength().ShouldBe(0);
        changes.RootElement.GetProperty("deletedContentIds").GetArrayLength().ShouldBe(0);
        // Modules kosulsuz doludur - "guncelsin" hata degil en mesru cevaptir.
        changes.RootElement.GetProperty("modules").GetArrayLength().ShouldBe(1);
        changes.RootElement.GetProperty("addedMedia").GetArrayLength().ShouldBe(0);
        changes.RootElement.GetProperty("removedMediaIds").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetChanges_SingleContentChanged_ReturnsOnlyThatContentVerbatim()
    {
        var bookId = await CreateBookAsync("Modül", "A", "B");
        await PublishAsync(bookId);
        var contentAId = await GetContentIdAsync(bookId, "A");

        await MutateContentAsync(bookId, "A", c => c.Title = "A - değişti");
        await PublishAsync(bookId);

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var upserted = changes.RootElement.GetProperty("upsertedContents").EnumerateArray().ToList();
        upserted.Count.ShouldBe(1);
        changes.RootElement.GetProperty("deletedContentIds").GetArrayLength().ShouldBe(0);

        // Delta-verbatim kaniti: parcanin ham metni, DB satirinin PayloadJson'iyla
        // TAM esit - deserialize/re-serialize hicbir asamada olmadi.
        var storedPayload = await GetPublishedContentPayloadAsync(bookId, contentAId, version: 2);
        upserted[0].GetRawText().ShouldBe(storedPayload);
    }

    [Fact]
    public async Task GetChanges_ContentChangedAcrossMultipleVersions_CollapsesToLatestVersionOnly()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId); // v1: "A"
        var contentAId = await GetContentIdAsync(bookId, "A");

        await MutateContentAsync(bookId, "A", c => c.Title = "A - orta");
        await PublishAsync(bookId); // v2

        await MutateContentAsync(bookId, "A - orta", c => c.Title = "A - son");
        await PublishAsync(bookId); // v3

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var upserted = changes.RootElement.GetProperty("upsertedContents").EnumerateArray().ToList();
        upserted.Count.ShouldBe(1);

        var v3Payload = await GetPublishedContentPayloadAsync(bookId, contentAId, version: 3);
        upserted[0].GetRawText().ShouldBe(v3Payload);
        upserted[0].GetProperty("title").GetString().ShouldBe("A - son");
    }

    [Fact]
    public async Task GetChanges_NewContentAdded_AppearsInUpserted()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId); // v1

        await AddContentAsync(bookId, "B");
        await PublishAsync(bookId); // v2

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var titles = changes.RootElement.GetProperty("upsertedContents").EnumerateArray()
            .Select(e => e.GetProperty("title").GetString())
            .ToList();

        titles.ShouldBe(["B"]);
    }

    [Fact]
    public async Task GetChanges_UnchangedContent_IsAbsentFromDelta()
    {
        // Journal modelinin uctan uca kaniti: "delta = tam indirme" DEGIL.
        var bookId = await CreateBookAsync("Modül", "A", "Sabit1", "Sabit2");
        await PublishAsync(bookId); // v1

        await MutateContentAsync(bookId, "A", c => c.Title = "A - değişti");
        await PublishAsync(bookId); // v2

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var titles = changes.RootElement.GetProperty("upsertedContents").EnumerateArray()
            .Select(e => e.GetProperty("title").GetString())
            .ToList();

        titles.ShouldBe(["A - değişti"]);
        titles.ShouldNotContain("Sabit1");
        titles.ShouldNotContain("Sabit2");
    }

    [Fact]
    public async Task GetChanges_ContentDeleted_AppearsInDeletedNotUpserted()
    {
        var bookId = await CreateBookAsync("Modül", "A", "B");
        await PublishAsync(bookId); // v1
        var contentBId = await GetContentIdAsync(bookId, "B");

        await DeleteContentAsync(bookId, "B");
        await PublishAsync(bookId); // v2

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var deletedIds = changes.RootElement.GetProperty("deletedContentIds").EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();
        deletedIds.ShouldBe([contentBId]);

        // A degismedi - journal bos, upserted'da hicbiri yok.
        changes.RootElement.GetProperty("upsertedContents").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetChanges_TombstoneAlreadySeen_IsNotRepeated()
    {
        var bookId = await CreateBookAsync("Modül", "A", "B");
        await PublishAsync(bookId); // v1
        await DeleteContentAsync(bookId, "B");
        await PublishAsync(bookId); // v2 - tombstone burada yazildi
        await PublishAsync(bookId); // v3 - hicbir degisiklik yok

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 2);

        changes.RootElement.GetProperty("deletedContentIds").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetChanges_ContentResurrectedAfterDeletion_AppearsInUpsertedNotDeleted()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId); // v1
        var contentAId = await GetContentIdAsync(bookId, "A");

        await DeleteContentAsync(bookId, "A");
        await PublishAsync(bookId); // v2 - tombstone

        await ResurrectContentAsync(contentAId);
        await PublishAsync(bookId); // v3 - "A" tekrar canli

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var upsertedIds = changes.RootElement.GetProperty("upsertedContents").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32())
            .ToList();
        upsertedIds.ShouldBe([contentAId]);
        changes.RootElement.GetProperty("deletedContentIds").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetChanges_MediaAddedChangedAndRemoved_ReflectsAllThreeInSingleDiff()
    {
        var bookId = await CreateBookAsync("Modül");
        var (mediaAId, _) = await CreateContentWithMediaAsync(bookId, "C1", "checksum-a");
        var (mediaBId, _) = await CreateContentWithMediaAsync(bookId, "C2", "checksum-b");
        await PublishAsync(bookId); // v1: media [A, B]

        await UpdateMediaChecksumAsync(mediaAId, "checksum-a-v2"); // A degisti
        await DeleteContentAsync(bookId, "C2"); // B'nin tek referansi gitti
        var (mediaCId, _) = await CreateContentWithMediaAsync(bookId, "C3", "checksum-c"); // yeni medya
        await PublishAsync(bookId); // v2: media [A(yeni checksum), C] - B yok

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var addedIds = changes.RootElement.GetProperty("addedMedia").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32())
            .ToList();
        addedIds.ShouldBe([mediaAId, mediaCId], ignoreOrder: true);

        var removedIds = changes.RootElement.GetProperty("removedMediaIds").EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();
        removedIds.ShouldBe([mediaBId]);
    }

    [Fact]
    public async Task GetChanges_FromVersionZero_ReturnsAllLiveContentPlusHistoricalTombstones()
    {
        var bookId = await CreateBookAsync("Modül", "A", "B");
        await PublishAsync(bookId); // v1
        var contentAId = await GetContentIdAsync(bookId, "A");

        await DeleteContentAsync(bookId, "A");
        await PublishAsync(bookId); // v2 - A silindi

        await AddContentAsync(bookId, "C");
        await PublishAsync(bookId); // v3 - C eklendi

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 0);

        var upsertedTitles = changes.RootElement.GetProperty("upsertedContents").EnumerateArray()
            .Select(e => e.GetProperty("title").GetString())
            .ToList();
        upsertedTitles.ShouldBe(["B", "C"], ignoreOrder: true);

        var deletedIds = changes.RootElement.GetProperty("deletedContentIds").EnumerateArray()
            .Select(e => e.GetInt32())
            .ToList();
        deletedIds.ShouldBe([contentAId]);
    }

    [Fact]
    public async Task GetChanges_FromVersionGreaterThanCurrent_ReturnsInvalidFromVersion()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId); // v1

        var result = await GetChangesResultAsync(bookId, fromVersion: 5);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Code.ShouldBe("Sync.InvalidFromVersion");
    }

    [Fact]
    public async Task GetChanges_NegativeFromVersion_ReturnsInvalidFromVersion()
    {
        var bookId = await CreateBookAsync("Modül", "A");
        await PublishAsync(bookId); // v1

        var result = await GetChangesResultAsync(bookId, fromVersion: -1);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe("Sync.InvalidFromVersion");
    }

    [Fact]
    public async Task GetChanges_BookNeverPublished_ReturnsNotPublished()
    {
        // Kendi kitabini yarat, publish ETME. (Seed kitap artik startup'ta
        // otomatik publish ediliyor - bkz. SeedPublisherExtensions - bu yuzden
        // "hic yayinlanmamis kitap" fixture'i olarak kullanilamaz.)
        var bookId = await CreateBookAsync("Modül", "A");

        var result = await GetChangesResultAsync(bookId, fromVersion: 0);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Sync.NotPublished");
    }

    [Fact]
    public async Task GetChanges_BookDoesNotExist_ReturnsBookNotFound()
    {
        var result = await GetChangesResultAsync(bookId: 999_999, fromVersion: 0);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Code.ShouldBe("Sync.BookNotFound");
    }

    [Fact]
    public async Task GetChanges_ModuleRenamed_ReflectsNewNameInModules()
    {
        var bookId = await CreateBookAsync("Eski Ad", "A");
        await PublishAsync(bookId); // v1

        await RenameModuleAsync(bookId, "Yeni Ad");
        await PublishAsync(bookId); // v2 - icerik degismedi, sadece modul adi

        using var changes = await GetChangesDocumentAsync(bookId, fromVersion: 1);

        var moduleNames = changes.RootElement.GetProperty("modules").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        moduleNames.ShouldBe(["Yeni Ad"]);
    }

    // ---- Yardimcilar ----

    private async Task<int> CreateBookAsync(string moduleName, params string[] contentTitles)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var module = new Module { Name = moduleName, DisplayOrder = 1 };
        for (var i = 0; i < contentTitles.Length; i++)
        {
            module.Contents.Add(new Content { Title = contentTitles[i], DisplayOrder = i + 1 });
        }

        var book = new Book
        {
            Title = "Delta Test Kitabı",
            Slug = $"delta-test-{Guid.NewGuid():N}",
            Modules = { module },
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }

    private async Task<int> GetContentIdAsync(int bookId, string title)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var content = await dbContext.Set<Content>()
            .Where(c => c.Module.BookId == bookId && c.Title == title)
            .SingleAsync();
        return content.Id;
    }

    private async Task PublishAsync(int bookId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var adminId = (await dbContext.Users.FirstAsync(u => u.UserName == "admin")).Id;

        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        var result = await publishingService.PublishAsync(bookId, adminId);
        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
    }

    private async Task MutateContentAsync(int bookId, string title, Action<Content> mutate)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
        var content = book!.Modules.SelectMany(m => m.Contents).Single(c => c.Title == title);
        mutate(content);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task AddContentAsync(int bookId, string title)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
        var module = book!.Modules.Single();
        // "Contents.Count + 1" onceki silmelerden sonra yanlis: silinen
        // content'ler goruntude kaybolur ama DisplayOrder'i canli kalan
        // kardesinde hala dolu olabilir (orn. A silinince Count=1 olur ama
        // B zaten DisplayOrder=2'yi tutuyordur). Max+1 bosluklardan bagimsiz
        // her zaman kullanilmamis bir deger uretir.
        var nextOrder = module.Contents.Count == 0 ? 1 : module.Contents.Max(c => c.DisplayOrder) + 1;
        module.Contents.Add(new Content { Title = title, DisplayOrder = nextOrder });
        await unitOfWork.SaveChangesAsync();
    }

    private async Task DeleteContentAsync(int bookId, string title)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
        var content = book!.Modules.SelectMany(m => m.Contents).Single(c => c.Title == title);
        unitOfWork.Contents.Remove(content);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Draft-seviyesinde "geri alma" - business katmaninda boyle bir servis
    /// yok (kapsam disi), bu yuzden query filter'i bilerek atlayip dogrudan
    /// DbContext ile yaziyoruz. Diriliş senaryosunu kurmanin tek yolu bu.
    /// </summary>
    private async Task ResurrectContentAsync(int contentId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var content = await dbContext.Set<Content>().IgnoreQueryFilters().SingleAsync(c => c.Id == contentId);
        content.IsDeleted = false;
        content.DeletedAt = null;
        await dbContext.SaveChangesAsync();
    }

    private async Task RenameModuleAsync(int bookId, string newName)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
        book!.Modules.Single().Name = newName;
        await unitOfWork.SaveChangesAsync();
    }

    private async Task<(int MediaId, string Checksum)> CreateContentWithMediaAsync(int bookId, string contentTitle, string mediaChecksum)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
        var module = book!.Modules.Single();

        var media = new Media
        {
            FileName = $"{contentTitle}.jpg",
            StoragePath = $"/media/{contentTitle}.jpg",
            MediaType = MediaType.Image,
            ContentType = "image/jpeg",
            FileSize = 1024,
            Checksum = mediaChecksum,
        };

        module.Contents.Add(new Content
        {
            Title = contentTitle,
            DisplayOrder = module.Contents.Count + 1,
            Blocks = { new ContentBlock { Type = ContentBlockType.Image, DisplayOrder = 1, Media = media } },
        });

        await unitOfWork.SaveChangesAsync();
        return (media.Id, mediaChecksum);
    }

    private async Task UpdateMediaChecksumAsync(int mediaId, string newChecksum)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var media = await dbContext.Set<Media>().SingleAsync(m => m.Id == mediaId);
        media.Checksum = newChecksum;
        await dbContext.SaveChangesAsync();
    }

    private async Task<string> GetPublishedContentPayloadAsync(int bookId, int contentId, int version)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        return await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId && pc.ContentId == contentId && pc.Version == version)
            .Select(pc => pc.PayloadJson)
            .SingleAsync();
    }

    private async Task<Result<string>> GetChangesResultAsync(int bookId, int fromVersion)
    {
        using var scope = factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
        return await syncService.GetChangesAsync(bookId, fromVersion);
    }

    private async Task<JsonDocument> GetChangesDocumentAsync(int bookId, int fromVersion)
    {
        var result = await GetChangesResultAsync(bookId, fromVersion);
        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        return JsonDocument.Parse(result.Value);
    }
}
