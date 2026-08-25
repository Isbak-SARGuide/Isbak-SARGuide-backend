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
    public async Task GetManifest_BookNeverPublished_Returns404WithNotPublishedCode()
    {
        // Arrange - seed kitap (id 1) hic publish edilmez (test kurali) -
        // "kitap var ama yayin yok" durumunun hazir temsilcisi.
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/sync/manifest?bookId=1");

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

        var module = new Module { Name = "Sync Modülü", DisplayOrder = 1 };
        module.Contents.Add(new Content { Title = "Sync İçeriği", DisplayOrder = 1 });

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
