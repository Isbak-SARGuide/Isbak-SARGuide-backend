using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Media;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Content.Enums;
using Isbak_SAR_Guide.Tests.Unit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 6 (Media Pipeline) guvenlik/davranis testleri - ApiFactory gercek
/// Postgres + izole bir temp storage klasoru kullanir (bkz. ApiFactory),
/// bu yuzden LocalFileStorageService gercekten diske yazar/siler; mock yok.
/// PNG govdeleri ImageSignatureDetectorTests'teki BuildMinimalPng ile
/// uretilir - gercek bir imaj kutuphanesi gerekmez, sadece Detect/IHDR
/// okumasinin ihtiyac duydugu baytlar dolu olsun yeter.
/// </summary>
[Collection("Api")]
public class MediaServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task UploadAsync_WithValidPng_CreatesMediaAndWritesFileToDisk()
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(10, 20);

        var result = await UploadAsync(bytes, "foto.png");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/png");
        result.Value.Width.ShouldBe(10);
        result.Value.Height.ShouldBe(20);
        result.Value.FileSize.ShouldBe(bytes.LongLength);

        PhysicalFileExists(result.Value.StoragePath).ShouldBeTrue();
    }

    [Fact]
    public async Task UploadAsync_WithValidJpeg_DetectsTypeAndReadsDimensions()
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalJpeg(640, 480);

        var result = await UploadAsync(bytes, "foto.jpg");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/jpeg");
        result.Value.Width.ShouldBe(640);
        result.Value.Height.ShouldBe(480);
    }

    [Fact]
    public async Task UploadAsync_WithValidGif_DetectsTypeAndReadsDimensions()
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalGif(64, 32);

        var result = await UploadAsync(bytes, "animasyon.gif");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/gif");
        result.Value.Width.ShouldBe(64);
        result.Value.Height.ShouldBe(32);
    }

    [Fact]
    public async Task UploadAsync_WithNonImageBytes_ReturnsValidationError()
    {
        byte[] bytes = [0x01, 0x02, 0x03, 0x04, 0x05];

        var result = await UploadAsync(bytes, "not-an-image.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_IgnoresDeclaredFileNameExtension_TrustsDetectedContentType()
    {
        // Dosya adi ".txt" diyor ama baytlar gercek bir PNG - detector'in
        // uzantiyi degil imzayi esas aldigini kanitlar.
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(4, 4);

        var result = await UploadAsync(bytes, "aslinda-metin.txt");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task UploadAsync_ExceedingConfiguredMaxSize_ReturnsValidationError()
    {
        long maxSize;
        using (var scope = factory.Services.CreateScope())
        {
            maxSize = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.MaxFileSizeBytes;
        }

        var oversized = new byte[maxSize + 1];

        var result = await UploadAsync(oversized, "buyuk.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_ReturnsValidationError()
    {
        var result = await UploadAsync([], "bos.png");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public async Task UploadAsync_DuplicateContent_ReturnsSameMediaWithoutWritingSecondFile()
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(2, 2);

        var first = await UploadAsync(bytes, "birinci.png");
        var second = await UploadAsync(bytes, "ikinci.png");

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        second.Value.Id.ShouldBe(first.Value.Id);
        second.Value.StoragePath.ShouldBe(first.Value.StoragePath);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var count = await dbContext.Set<Media>().CountAsync(m => m.Checksum == first.Value.Checksum);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenReferencedByContentBlock_ReturnsConflict()
    {
        var mediaId = await CreateReferencedMediaAsync();

        var result = await DeleteAsync(mediaId);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotReferenced_RemovesRowAndPhysicalFile()
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(1, 1);
        var uploaded = await UploadAsync(bytes, "silinecek.png");
        uploaded.IsSuccess.ShouldBeTrue();

        var deleteResult = await DeleteAsync(uploaded.Value.Id);
        deleteResult.IsSuccess.ShouldBeTrue();

        PhysicalFileExists(uploaded.Value.StoragePath).ShouldBeFalse();

        var getResult = await GetByIdAsync(uploaded.Value.Id);
        getResult.IsFailure.ShouldBeTrue();
        getResult.Error!.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task CleanupOrphansAsync_RemovesOnlyOldUnreferencedMedia()
    {
        // Farkli boyutlar kasitli: BuildMinimalPng'in urettigi baytlar sadece
        // genislik/yukseklige gore degisiyor - ayni boyut ayni checksum (dedup)
        // demek, testin "iki bagimsiz Media satiri" varsayimini bozardi.
        var oldOrphanId = await UploadAndBackdateAsync("eski-yetim.png", width: 101, hoursAgo: 48);
        var recentOrphanId = await UploadAndBackdateAsync("yeni-yetim.png", width: 102, hoursAgo: 1);
        var oldReferencedId = await CreateReferencedMediaAsync(backdateHoursAgo: 48);

        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var cleanupResult = await mediaService.CleanupOrphansAsync();

        cleanupResult.IsSuccess.ShouldBeTrue();
        cleanupResult.Value.ShouldBe(1);

        (await mediaService.GetByIdAsync(oldOrphanId)).IsFailure.ShouldBeTrue();
        (await mediaService.GetByIdAsync(recentOrphanId)).IsSuccess.ShouldBeTrue();
        (await mediaService.GetByIdAsync(oldReferencedId)).IsSuccess.ShouldBeTrue();
    }

    // ---- Yardımcılar ----

    private async Task<Business.Common.Result<MediaDto>> UploadAsync(byte[] bytes, string fileName)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        using var stream = new MemoryStream(bytes);
        return await mediaService.UploadAsync(stream, fileName, bytes.LongLength);
    }

    private async Task<Business.Common.Result<MediaDto>> GetByIdAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        return await mediaService.GetByIdAsync(id);
    }

    private async Task<Business.Common.Result> DeleteAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();
        return await mediaService.DeleteAsync(id);
    }

    private async Task<int> UploadAndBackdateAsync(string fileName, int width, int hoursAgo)
    {
        var bytes = ImageSignatureDetectorTests.BuildMinimalPng(width, height: 1);
        var uploaded = await UploadAsync(bytes, fileName);
        uploaded.IsSuccess.ShouldBeTrue();

        await BackdateAsync(uploaded.Value.Id, hoursAgo);
        return uploaded.Value.Id;
    }

    /// <summary>Bir ContentBlock'un referans verdigi, dilerse geriye tarihli bir Media kaydi olusturur.</summary>
    private async Task<int> CreateReferencedMediaAsync(int? backdateHoursAgo = null)
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book { Title = "Media Test Kitabı", Slug = $"media-test-{Guid.NewGuid():N}" };
        var module = new Module { Name = "Test Modülü", DisplayOrder = 0 };
        var media = new Media
        {
            FileName = "referenced.png",
            StoragePath = $"media/test/{Guid.NewGuid():N}.png",
            MediaType = MediaType.Image,
            ContentType = "image/png",
            FileSize = 1,
            Checksum = Guid.NewGuid().ToString("N"),
        };
        var content = new Content
        {
            Title = "Test İçeriği",
            DisplayOrder = 0,
            Blocks = { new ContentBlock { Type = ContentBlockType.Image, DisplayOrder = 0, Media = media } },
        };
        module.Contents.Add(content);
        book.Modules.Add(module);

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();

        if (backdateHoursAgo is { } hours)
        {
            await BackdateAsync(media.Id, hours);
        }

        return media.Id;
    }

    private async Task BackdateAsync(int mediaId, int hoursAgo)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();
        var media = await dbContext.Set<Media>().SingleAsync(m => m.Id == mediaId);
        media.CreatedAt = DateTime.UtcNow.AddHours(-hoursAgo);
        await dbContext.SaveChangesAsync();
    }

    private bool PhysicalFileExists(string storagePath)
    {
        using var scope = factory.Services.CreateScope();
        var basePath = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.BasePath;
        return File.Exists(Path.Combine(basePath, storagePath));
    }
}
