using System.Net;
using System.Net.Http.Json;
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
/// 7.1: /sync/manifest artik yayin tablosundan VERBATIM okur. Asil kanit ilk
/// testteki tam string esitligi - govde, DB'deki ManifestJson ile bayt bayt
/// ayni olmali (Contains degil); arada herhangi bir deserialize/re-serialize
/// olsaydi encoder farki esitligi bozardi. Endpoint anonim (mobil sozlesmesi).
/// </summary>
[Collection("Api")]
public class SyncManifestTests(ApiFactory factory)
{
    [Fact]
    public async Task GetManifest_AfterPublish_ReturnsStoredManifestVerbatim()
    {
        // Arrange - kendi kitabini yarat ve publish et
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // ContentResult yanlis kurulursa text/plain donerdi - ucuz sigorta.
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

        var body = await response.Content.ReadAsStringAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var storedManifest = await dbContext.Set<BookPublication>()
            .Where(p => p.BookId == bookId)
            .Select(p => p.ManifestJson)
            .SingleAsync();

        // Verbatim'in kaniti: tam esitlik, Contains degil.
        body.ShouldBe(storedManifest);
    }

    [Fact]
    public async Task GetManifest_MatchingIfNoneMatch_Returns304WithoutBody()
    {
        // Arrange
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();
        var firstResponse = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");
        var eTag = firstResponse.Headers.ETag!.ToString();

        // Act - ayni ETag'i If-None-Match olarak geri gonder
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sync/manifest?bookId={bookId}");
        request.Headers.TryAddWithoutValidation("If-None-Match", eTag);
        var response = await client.SendAsync(request);

        // Assert - 304, govde yok
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetManifest_StaleIfNoneMatch_Returns200WithBody()
    {
        // Arrange
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();

        // Act - eski/uydurma bir ETag gonder
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sync/manifest?bookId={bookId}");
        request.Headers.TryAddWithoutValidation("If-None-Match", "W/\"999999.999999\"");
        var response = await client.SendAsync(request);

        // Assert - normal 200 + govde, yeni ETag baslikta
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetManifest_AfterRepublish_ReturnsFreshVersionNotStale()
    {
        // Arrange - 12.2 cache'in asil kritik ozelligi: republish sonrasi
        // bayat (v1) degil taze (v2) manifest gelmeli.
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();

        var firstResponse = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        firstBody.ShouldContain("\"version\":1");

        // Act - tekrar yayinla (icerik degismese bile versiyon her zaman +1 olur)
        await PublishAsync(bookId);
        var secondResponse = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        // Assert - cache invalidation calismasaydi burasi hala v1 dönerdi.
        secondBody.ShouldContain("\"version\":2");
        secondBody.ShouldNotBe(firstBody);
    }

    [Fact]
    public async Task GetManifest_BookNeverPublished_Returns404WithNotPublishedCode()
    {
        // Arrange - kendi kitabini yarat, publish ETME. (Seed kitap artik
        // startup'ta otomatik publish ediliyor - bkz. SeedPublisherExtensions -
        // bu yuzden "hic yayinlanmamis kitap" fixture'i olarak kullanilamaz.)
        var bookId = await CreateBookAsync();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/sync/manifest?bookId={bookId}");

        // Assert - 404 ama kod ayirt edici: mobil "icerik hazirlaniyor" gosterir.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.ShouldBe("Sync.NotPublished");
    }

    [Fact]
    public async Task GetManifest_BookDoesNotExist_Returns404WithBookNotFoundCode()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/sync/manifest?bookId=999999");

        // Assert - ayni 404, farkli kod: bu bir konfigurasyon hatasi.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Title.ShouldBe("Sync.BookNotFound");
    }

    // ---- Yardimcilar ----

    private async Task<int> CreateBookAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Faz 13.3: IsPublished=true bilincli - bu testler manifest/publish
        // motorunu test ediyor, IsPublished filtresinin kendisini degil.
        var module = new Module { Name = "Sync Modülü", DisplayOrder = 1, IsPublished = true };
        module.Contents.Add(new Content { Title = "Sync İçeriği", DisplayOrder = 1, IsPublished = true });

        var book = new Book
        {
            Title = "Sync Manifest Kitabı",
            Slug = $"sync-manifest-{Guid.NewGuid():N}",
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
