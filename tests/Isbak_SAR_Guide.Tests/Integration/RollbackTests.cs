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
/// Faz 12.6: PublishingService.RollbackAsync. PublishingTests.cs'teki desen
/// ve gerekce ayni (dogrudan IPublishingService, gercek Postgres). Rollback
/// "geri alma" degil "eski icerigi YENI bir versiyon olarak tekrar yayinlama" -
/// immutable publication modelinin dogal sonucu, git revert gibi.
/// </summary>
[Collection("Api")]
public class RollbackTests(ApiFactory factory)
{
    [Fact]
    public async Task RollbackAsync_ToOlderVersion_CreatesNewVersionWithOldContentAndBumpsBookVersion()
    {
        // Arrange - v1 "Orijinal", v2 "Değişmiş"
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);
        await MutateFirstContentAsync(bookId, c => c.Title = "Değişmiş Başlık");
        await PublishAsync(bookId, adminId);

        // Act - v1'e geri don
        var result = await RollbackAsync(bookId, toVersion: 1, adminId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Version.ShouldBe(3);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var v3Publication = await dbContext.Set<BookPublication>()
            .SingleAsync(p => p.BookId == bookId && p.Version == 3);
        v3Publication.SnapshotJson.ShouldContain("Orijinal Başlık");
        v3Publication.SnapshotJson.ShouldNotContain("Değişmiş Başlık");

        // v3'un ICINDEKI Version alani da 3 olmali - v1'in eski gomulu
        // numarasi (1) degil, republish anindaki gercek numara.
        v3Publication.SnapshotJson.ShouldContain("\"version\":3");

        // Journal: v2'de degisen tek content, v3'te ESKI haline geri
        // donduğu icin yine degisti sayilir - bir satir yazilmali.
        var v3Rows = await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId && pc.Version == 3)
            .ToListAsync();
        v3Rows.Count.ShouldBe(1);
        v3Rows[0].PayloadJson.ShouldContain("Orijinal Başlık");

        var book = await dbContext.Set<Book>().SingleAsync(b => b.Id == bookId);
        book.Version.ShouldBe(3);

        // Draft agacina DOKUNULMAMIS olmali - CMS'teki taslak hala "Değişmiş".
        var draftContent = await dbContext.Set<Content>()
            .Include(c => c.Module)
            .SingleAsync(c => c.Module!.BookId == bookId);
        draftContent.Title.ShouldBe("Değişmiş Başlık");
    }

    [Fact]
    public async Task RollbackAsync_PreservesIntermediateVersions()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);
        await MutateFirstContentAsync(bookId, c => c.Title = "Değişmiş Başlık");
        await PublishAsync(bookId, adminId);

        // Act
        await RollbackAsync(bookId, toVersion: 1, adminId);

        // Assert - v1 ve v2 immutable, hala oldugu gibi duruyor.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var v1 = await dbContext.Set<BookPublication>().SingleAsync(p => p.BookId == bookId && p.Version == 1);
        var v2 = await dbContext.Set<BookPublication>().SingleAsync(p => p.BookId == bookId && p.Version == 2);
        v1.SnapshotJson.ShouldContain("Orijinal Başlık");
        v2.SnapshotJson.ShouldContain("Değişmiş Başlık");
    }

    [Fact]
    public async Task RollbackAsync_RestoresContentDeletedAfterTargetVersion()
    {
        // Arrange - v1: "Kalan" + "Silinecek". Sonra "Silinecek" soft-delete
        // edilip v2 yayinlanir (tombstone). v1'e rollback, "Silinecek"i
        // DIRILTMELI - eski snapshot'ta hala var.
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
            unitOfWork.Contents.Remove(doomed);
            await unitOfWork.SaveChangesAsync();
        }

        await PublishAsync(bookId, adminId); // v2: tombstone

        // Act
        var result = await RollbackAsync(bookId, toVersion: 1, adminId);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        using var scope2 = factory.Services.CreateScope();
        var dbContext = scope2.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var v3Row = await dbContext.Set<PublishedContent>()
            .SingleAsync(pc => pc.BookId == bookId && pc.Version == 3 && pc.ContentId == deletedContentId);
        v3Row.IsDeleted.ShouldBeFalse();
        v3Row.PayloadJson.ShouldContain("Silinecek");

        var v3Publication = await dbContext.Set<BookPublication>().SingleAsync(p => p.BookId == bookId && p.Version == 3);
        v3Publication.SnapshotJson.ShouldContain("Silinecek");
    }

    [Fact]
    public async Task RollbackAsync_ToCurrentVersion_ReturnsValidationError()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        // Act - mevcut = hedef (1 -> 1), geriye gitme yok.
        var result = await RollbackAsync(bookId, toVersion: 1, adminId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task RollbackAsync_ToNonExistentVersion_ReturnsNotFound()
    {
        // Arrange
        var bookId = await CreateBookWithContentsAsync("İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        // Act
        var result = await RollbackAsync(bookId, toVersion: 999, adminId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task RollbackAsync_BookDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var adminId = await GetAdminIdAsync();

        // Act
        var result = await RollbackAsync(bookId: 999_999, toVersion: 1, adminId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    // ---- Yardımcılar ----

    private async Task<Result<Business.DTOs.Publishing.PublishResultDto>> PublishAsync(int bookId, string adminId)
    {
        using var scope = factory.Services.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        return await publishingService.PublishAsync(bookId, adminId);
    }

    private async Task<Result<Business.DTOs.Publishing.PublishResultDto>> RollbackAsync(int bookId, int toVersion, string adminId)
    {
        using var scope = factory.Services.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        return await publishingService.RollbackAsync(bookId, toVersion, adminId);
    }

    private async Task<int> CreateBookWithContentsAsync(params string[] contentTitles)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Faz 13.3: IsPublished=true bilincli - bu testler rollback motorunu
        // test ediyor, IsPublished filtresinin kendisini degil.
        var module = new Module { Name = "Rollback Test Modülü", DisplayOrder = 1, IsPublished = true };

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
            Title = "Rollback Test Kitabı",
            Slug = $"rollback-test-{Guid.NewGuid():N}",
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

    private async Task<string> GetAdminIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var admin = await dbContext.Users.FirstAsync(u => u.UserName == "admin");
        return admin.Id;
    }
}
