using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// 7.2: /sync/snapshot yayin tablosundan VERBATIM okur. Tacin ucu son test:
/// mobilin cihazda yapacagi butunluk dogrulamasinin (manifest'ten checksum al,
/// snapshot'i indir, hash'le, karsilastir) iki gercek HTTP ucu uzerinden
/// uctan uca provasi. Manifest'i PARSE etmek serbest - verbatim kurali servis
/// etmeyi kisitlar, okumayi degil; istemciler de parse eder, etmek zorundadir.
/// </summary>
[Collection("Api")]
public class SyncSnapshotTests(ApiFactory factory)
{
    [Fact]
    public async Task GetSnapshot_AfterPublish_ReturnsStoredSnapshotVerbatim()
    {
        // Arrange
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/sync/snapshot?bookId={bookId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadAsStringAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var storedSnapshot = await dbContext.Set<BookPublication>()
            .Where(p => p.BookId == bookId)
            .Select(p => p.SnapshotJson)
            .SingleAsync();

        // Verbatim'in kaniti: tam esitlik, Contains degil.
        body.ShouldBe(storedSnapshot);
    }

    [Fact]
    public async Task GetSnapshot_BookNeverPublished_Returns404WithNotPublishedCode()
    {
        // Arrange - seed kitap (id 1) hic publish edilmez (test kurali).
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/sync/snapshot?bookId=1");

        // Assert - manifest ile AYNI kod: kod, ucun degil gercegin adi.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.ShouldBe("Sync.NotPublished");
    }

    [Fact]
    public async Task GetSnapshot_BookDoesNotExist_Returns404WithBookNotFoundCode()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/sync/snapshot?bookId=999999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.ShouldBe("Sync.BookNotFound");
    }

    [Fact]
    public async Task SyncFlow_SnapshotBytes_MatchManifestChecksumEndToEnd()
    {
        // Arrange
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();

        // Act 1 - manifest'i cek ve PARSE et (JsonDocument: salt-okur API,
        // "buradan bir sey geri yazilmiyor" niyeti kodda da belli).
        var manifestResponse = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");
        manifestResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var manifest = JsonDocument.Parse(await manifestResponse.Content.ReadAsStringAsync());
        // Kanonik form camelCase - "checksum", "Checksum" degil.
        var expectedChecksum = manifest.RootElement.GetProperty("checksum").GetString();
        var manifestVersion = manifest.RootElement.GetProperty("version").GetInt32();

        // Act 2 - snapshot'i indir.
        var snapshotResponse = await client.GetAsync($"/api/v1/sync/snapshot?bookId={bookId}");
        snapshotResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var snapshotBody = await snapshotResponse.Content.ReadAsStringAsync();

        // Assert - mobilin yapacagi dogrulamanin birebir kendisi. Hash bilerek
        // bagimsiz hesaplanir (SnapshotBuilder.ComputeChecksum DEGIL) - test,
        // uretim kodunu ayni fonksiyonla kendi kendine onaylatmaz.
        var actualChecksum = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(snapshotBody)));

        actualChecksum.ShouldBe(expectedChecksum);

        using var snapshot = JsonDocument.Parse(snapshotBody);
        snapshot.RootElement.GetProperty("version").GetInt32().ShouldBe(manifestVersion);
    }

    // ---- Yardimcilar ----

    private async Task<int> CreateBookAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var module = new Module { Name = "Snapshot Modülü", DisplayOrder = 1 };
        module.Contents.Add(new Content { Title = "Snapshot İçeriği", DisplayOrder = 1 });

        var book = new Book
        {
            Title = "Sync Snapshot Kitabı",
            Slug = $"sync-snapshot-{Guid.NewGuid():N}",
            Modules = { module },
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
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
}
