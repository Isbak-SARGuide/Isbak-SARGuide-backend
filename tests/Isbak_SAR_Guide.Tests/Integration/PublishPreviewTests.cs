using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
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
/// Kullanicinin bulgusu (2026-09-02): Yayinla hicbir geri bildirim olmadan
/// direkt commit ediyordu. PreviewAsync SALT-OKUR - bu dosya, hicbir testinde
/// preview'un kendisinin bir BookPublication yazmadigini da ayrica dogrular
/// (asil kritik garanti).
/// </summary>
[Collection("Api")]
public class PublishPreviewTests(ApiFactory factory)
{
    [Fact]
    public async Task PreviewAsync_BookNeverPublished_ReturnsWholeDraftTreeAsAdded()
    {
        var bookId = await CreateBookWithContentsAsync("İçerik A", "İçerik B");

        var result = await PreviewAsync(bookId);

        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        result.Value.HasChanges.ShouldBeTrue();
        result.Value.BookMetadataChanged.ShouldBeFalse();
        result.Value.AddedModules.Count.ShouldBe(1);
        result.Value.AddedContents.Count.ShouldBe(2);
        result.Value.ChangedModules.ShouldBeEmpty();
        result.Value.ChangedContents.ShouldBeEmpty();
        result.Value.RemovedModules.ShouldBeEmpty();
        result.Value.RemovedContents.ShouldBeEmpty();

        (await CountPublicationsAsync(bookId)).ShouldBe(0);
    }

    [Fact]
    public async Task PreviewAsync_NoChangesSincePublish_ReturnsNoChanges()
    {
        var bookId = await CreateBookWithContentsAsync("Sabit İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        var result = await PreviewAsync(bookId);

        result.IsSuccess.ShouldBeTrue(result.Error?.Message);
        result.Value.HasChanges.ShouldBeFalse();
        result.Value.BookMetadataChanged.ShouldBeFalse();
        result.Value.AddedModules.ShouldBeEmpty();
        result.Value.ChangedModules.ShouldBeEmpty();
        result.Value.RemovedModules.ShouldBeEmpty();
        result.Value.AddedContents.ShouldBeEmpty();
        result.Value.ChangedContents.ShouldBeEmpty();
        result.Value.RemovedContents.ShouldBeEmpty();

        (await CountPublicationsAsync(bookId)).ShouldBe(1);
    }

    [Fact]
    public async Task PreviewAsync_ContentTitleChanged_ReturnsItInChangedContents()
    {
        var bookId = await CreateBookWithContentsAsync("Orijinal Başlık", "Sabit İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);
        var contentId = await MutateFirstContentAsync(bookId, c => c.Title = "Yeni Başlık");

        var result = await PreviewAsync(bookId);

        result.Value.HasChanges.ShouldBeTrue();
        result.Value.ChangedContents.Count.ShouldBe(1);
        result.Value.ChangedContents.Single().Id.ShouldBe(contentId);
        result.Value.ChangedContents.Single().Title.ShouldBe("Yeni Başlık");
        result.Value.AddedContents.ShouldBeEmpty();
        result.Value.RemovedContents.ShouldBeEmpty();

        // Onizleme hicbir sey yazmadi - hala tek yayin var (v1).
        (await CountPublicationsAsync(bookId)).ShouldBe(1);
    }

    [Fact]
    public async Task PreviewAsync_NewContentAddedAfterPublish_ReturnsItInAddedContents()
    {
        var bookId = await CreateBookWithContentsAsync("Mevcut İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        int newContentId;
        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId);
            var module = book!.Modules.Single();
            var content = new Content { Title = "Yeni İçerik", DisplayOrder = 2, IsPublished = true };
            module.Contents.Add(content);
            await unitOfWork.SaveChangesAsync();
            newContentId = content.Id;
        }

        var result = await PreviewAsync(bookId);

        result.Value.AddedContents.Count.ShouldBe(1);
        result.Value.AddedContents.Single().Id.ShouldBe(newContentId);
        result.Value.ChangedContents.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_ContentDeletedAfterPublish_ReturnsItInRemovedContents()
    {
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

        var result = await PreviewAsync(bookId);

        result.Value.RemovedContents.Count.ShouldBe(1);
        result.Value.RemovedContents.Single().Id.ShouldBe(deletedContentId);
        result.Value.AddedContents.ShouldBeEmpty();
        result.Value.ChangedContents.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_BookTitleChangedOnly_ReturnsBookMetadataChangedTrueWithEmptyLists()
    {
        var bookId = await CreateBookWithContentsAsync("Sabit İçerik");
        var adminId = await GetAdminIdAsync();
        await PublishAsync(bookId, adminId);

        using (var scope = factory.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var book = await unitOfWork.Books.FindByIdAsync(bookId);
            book!.Title = "Yeni Kitap Başlığı";
            await unitOfWork.SaveChangesAsync();
        }

        var result = await PreviewAsync(bookId);

        // Kritik: HasChanges true olmali BILE modul/icerik listeleri bos olsa -
        // aksi halde bu degisiklik onizlemede sessizce kaybolurdu.
        result.Value.BookMetadataChanged.ShouldBeTrue();
        result.Value.HasChanges.ShouldBeTrue();
        result.Value.AddedModules.ShouldBeEmpty();
        result.Value.ChangedModules.ShouldBeEmpty();
        result.Value.AddedContents.ShouldBeEmpty();
        result.Value.ChangedContents.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewAsync_BookDoesNotExist_ReturnsNotFound()
    {
        var result = await PreviewAsync(999_999);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    // ---- Yardımcılar ----

    private async Task<Result<PublishPreviewDto>> PreviewAsync(int bookId)
    {
        using var scope = factory.Services.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        return await publishingService.PreviewAsync(bookId);
    }

    private async Task<Result<PublishResultDto>> PublishAsync(int bookId, string adminId)
    {
        using var scope = factory.Services.CreateScope();
        var publishingService = scope.ServiceProvider.GetRequiredService<IPublishingService>();
        return await publishingService.PublishAsync(bookId, adminId);
    }

    private async Task<int> CountPublicationsAsync(int bookId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        return await dbContext.Set<BookPublication>().CountAsync(p => p.BookId == bookId);
    }

    private async Task<int> CreateBookWithContentsAsync(params string[] contentTitles)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

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
            Title = "Publish Preview Test Kitabı",
            Slug = $"publish-preview-test-{Guid.NewGuid():N}",
            Modules = { module },
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }

    private async Task<int> MutateFirstContentAsync(int bookId, Action<Content> mutate)
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
        return content.Id;
    }

    private async Task<string> GetAdminIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var admin = await dbContext.Users.FirstAsync(u => u.UserName == "admin");
        return admin.Id;
    }
}
