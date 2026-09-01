using System.Net;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// 12.4: /sync/changes'in ETag/If-None-Match davranisi - HTTP seviyesinde.
/// SyncChangesTests.cs delta MOTOR semantigini servisi dogrudan cagirarak
/// kanitlar (HTTP'nin bilgisi degil); bu dosya tam tersi - sadece HTTP
/// katmanindaki basligi test eder, delta icerigine hic girmez.
/// </summary>
[Collection("Api")]
public class SyncChangesEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GetChanges_MatchingIfNoneMatch_Returns304WithoutBody()
    {
        // Arrange
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId);
        var client = factory.CreateClient();
        var firstResponse = await client.GetAsync($"/api/v1/sync/changes?bookId={bookId}&fromVersion=0");
        var eTag = firstResponse.Headers.ETag!.ToString();

        // Act - ayni ETag'i If-None-Match olarak geri gonder
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sync/changes?bookId={bookId}&fromVersion=0");
        request.Headers.TryAddWithoutValidation("If-None-Match", eTag);
        var response = await client.SendAsync(request);

        // Assert - 304, govde yok
        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChanges_DifferentFromVersion_ProducesDifferentETag()
    {
        // Arrange - ayni kitap icin iki farkli fromVersion, ETag'in
        // fromVersion'i da kapsadigini kanitlar (sadece toVersion degil).
        var bookId = await CreateBookAsync();
        await PublishAsync(bookId); // v1
        await PublishAsync(bookId); // v2
        var client = factory.CreateClient();

        // Act
        var fromZero = await client.GetAsync($"/api/v1/sync/changes?bookId={bookId}&fromVersion=0");
        var fromOne = await client.GetAsync($"/api/v1/sync/changes?bookId={bookId}&fromVersion=1");

        // Assert
        fromZero.Headers.ETag!.ToString().ShouldNotBe(fromOne.Headers.ETag!.ToString());
    }

    // ---- Yardimcilar ----

    private async Task<int> CreateBookAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var module = new Module { Name = "Changes ETag Modülü", DisplayOrder = 1, IsPublished = true };
        module.Contents.Add(new Content { Title = "Changes ETag İçeriği", DisplayOrder = 1, IsPublished = true });

        var book = new Book
        {
            Title = "Sync Changes ETag Kitabı",
            Slug = $"sync-changes-etag-{Guid.NewGuid():N}",
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
